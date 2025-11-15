using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorLiveMentor.Models;
using OfficeOpenXml;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace TutorLiveMentor.Controllers
{
    public class FacultyReportsController : Controller
    {
        private readonly AppDbContext _context;

        public FacultyReportsController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Generate enrollment report for faculty's specific subject
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GenerateFacultyReport([FromBody] FacultyReportRequest request)
        {
            var facultyId = HttpContext.Session.GetInt32("FacultyId");
            if (facultyId == null)
                return Unauthorized();

            try
            {
                // Get the assigned subject IDs for this faculty and subject name
                var assignedSubjectIds = await _context.AssignedSubjects
                    .Include(a => a.Subject)
                    .Where(a => a.FacultyId == facultyId.Value && a.Subject.Name == request.SubjectName)
                    .Select(a => a.AssignedSubjectId)
                    .ToListAsync();

                if (assignedSubjectIds.Count == 0)
                {
                    return Json(new { success = true, data = new List<EnrollmentReportDto>(), message = "No assigned subjects found" });
                }

                // Get enrolled students with details
                var results = await _context.StudentEnrollments
                    .Include(se => se.Student)
                    .Include(se => se.AssignedSubject)
                        .ThenInclude(a => a.Subject)
                    .Include(se => se.AssignedSubject)
                        .ThenInclude(a => a.Faculty)
                    .Where(se => assignedSubjectIds.Contains(se.AssignedSubjectId))
                    .OrderBy(se => se.EnrolledAt)
                    .ThenBy(se => se.Student.FullName)
                    .Select(se => new EnrollmentReportDto
                    {
                        StudentName = se.Student.FullName,
                        StudentRegdNumber = se.Student.RegdNumber,
                        StudentEmail = se.Student.Email,
                        StudentYear = se.Student.Year,
                        SubjectName = se.AssignedSubject.Subject.Name,
                        FacultyName = se.AssignedSubject.Faculty.Name,
                        FacultyEmail = se.AssignedSubject.Faculty.Email,
                        EnrollmentDate = se.EnrolledAt.Date,
                        EnrolledAt = se.EnrolledAt,
                        Semester = se.AssignedSubject.Subject.Semester ?? ""
                    })
                    .ToListAsync();

                return Json(new { success = true, data = results });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GenerateFacultyReport: {ex}");
                return Json(new { success = false, message = $"Error generating report: {ex.Message}" });
            }
        }

        /// <summary>
        /// Export faculty report to Excel with column selection
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ExportFacultyReportExcel([FromBody] FacultyExportRequest request)
        {
            var facultyId = HttpContext.Session.GetInt32("FacultyId");
            if (facultyId == null)
                return Unauthorized();

            try
            {
                if (request?.ReportData == null || request.ReportData.Count == 0)
                {
                    return BadRequest("No data to export");
                }

                var reportData = request.ReportData;
                var columns = request.SelectedColumns ?? new FacultyColumnSelection();

                // Create Excel file
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Students Enrolled Report");

                // Build headers dynamically based on selected columns
                int colIndex = 1;
                var columnMapping = new Dictionary<string, int>();

                // FIXED ORDER: Registration Number FIRST, then Student Name
                if (columns.RegdNumber)
                {
                    worksheet.Cells[1, colIndex].Value = "Registration No";
                    columnMapping["RegdNumber"] = colIndex++;
                }
                if (columns.StudentName)
                {
                    worksheet.Cells[1, colIndex].Value = "Student Name";
                    columnMapping["StudentName"] = colIndex++;
                }
                if (columns.Email)
                {
                    worksheet.Cells[1, colIndex].Value = "Student Email";
                    columnMapping["Email"] = colIndex++;
                }
                if (columns.Year)
                {
                    worksheet.Cells[1, colIndex].Value = "Year";
                    columnMapping["Year"] = colIndex++;
                }
                if (columns.Department)
                {
                    worksheet.Cells[1, colIndex].Value = "Department";
                    columnMapping["Department"] = colIndex++;
                }
                if (columns.EnrollmentTime)
                {
                    worksheet.Cells[1, colIndex].Value = "Enrollment Time";
                    columnMapping["EnrollmentTime"] = colIndex++;
                }

                // Header styling
                using (var range = worksheet.Cells[1, 1, 1, colIndex - 1])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(111, 66, 193));
                    range.Style.Font.Color.SetColor(System.Drawing.Color.White);
                }

                // Data - FIXED ORDER: Registration Number FIRST, then Student Name
                for (int i = 0; i < reportData.Count; i++)
                {
                    var row = i + 2;
                    var item = reportData[i];
                    
                    if (columns.RegdNumber && columnMapping.ContainsKey("RegdNumber"))
                        worksheet.Cells[row, columnMapping["RegdNumber"]].Value = item.StudentRegdNumber;
                    
                    if (columns.StudentName && columnMapping.ContainsKey("StudentName"))
                        worksheet.Cells[row, columnMapping["StudentName"]].Value = item.StudentName;
                    
                    if (columns.Email && columnMapping.ContainsKey("Email"))
                        worksheet.Cells[row, columnMapping["Email"]].Value = item.StudentEmail;
                    
                    if (columns.Year && columnMapping.ContainsKey("Year"))
                        worksheet.Cells[row, columnMapping["Year"]].Value = item.StudentYear;
                    
                    if (columns.Department && columnMapping.ContainsKey("Department"))
                        worksheet.Cells[row, columnMapping["Department"]].Value = item.StudentYear; // Department can be derived from year
                    
                    if (columns.EnrollmentTime && columnMapping.ContainsKey("EnrollmentTime"))
                    {
                        // Convert UTC to local time and format with milliseconds
                        var localTime = item.EnrolledAt.ToLocalTime();
                        worksheet.Cells[row, columnMapping["EnrollmentTime"]].Value = 
                            localTime.ToString("MM/dd/yyyy hh:mm:ss.fff tt");
                    }
                }

                // Auto-fit columns
                worksheet.Cells.AutoFitColumns();

                // Add summary row
                int summaryRow = reportData.Count + 3;
                worksheet.Cells[summaryRow, 1].Value = "Total Students:";
                worksheet.Cells[summaryRow, 2].Value = reportData.Count;
                worksheet.Cells[summaryRow, 1, summaryRow, 2].Style.Font.Bold = true;

                // Generate file
                var fileName = $"Students_Enrolled_{request.SubjectName?.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var content = package.GetAsByteArray();

                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Excel export error: {ex}");
                return StatusCode(500, $"Error exporting to Excel: {ex.Message}");
            }
        }

        /// <summary>
        /// Export faculty report to PDF with column selection
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ExportFacultyReportPDF([FromBody] FacultyExportRequest request)
        {
            var facultyId = HttpContext.Session.GetInt32("FacultyId");
            if (facultyId == null)
                return Unauthorized();

            try
            {
                if (request?.ReportData == null || request.ReportData.Count == 0)
                {
                    return BadRequest("No data to export");
                }

                var reportData = request.ReportData;
                var columns = request.SelectedColumns ?? new FacultyColumnSelection();

                // Count selected columns
                int columnCount = 0;
                if (columns.RegdNumber) columnCount++;
                if (columns.StudentName) columnCount++;
                if (columns.Email) columnCount++;
                if (columns.Year) columnCount++;
                if (columns.Department) columnCount++;
                if (columns.EnrollmentTime) columnCount++;

                if (columnCount == 0)
                {
                    return BadRequest("No columns selected for export");
                }

                // Create PDF using iTextSharp
                using var stream = new MemoryStream();
                var document = new Document(PageSize.A4.Rotate(), 25, 25, 30, 30);
                var writer = PdfWriter.GetInstance(document, stream);
                
                document.Open();
                
                // Title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                var title = new Paragraph($"Students Enrolled in {request.SubjectName}", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                title.SpacingAfter = 10;
                document.Add(title);

                // Faculty info
                var facultyName = HttpContext.Session.GetString("FacultyName");
                var infoFont = FontFactory.GetFont(FontFactory.HELVETICA, 12);
                var info = new Paragraph($"Faculty: {facultyName}", infoFont);
                info.Alignment = Element.ALIGN_CENTER;
                info.SpacingAfter = 20;
                document.Add(info);

                // Date
                var dateFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                var dateText = new Paragraph($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", dateFont);
                dateText.Alignment = Element.ALIGN_RIGHT;
                dateText.SpacingAfter = 20;
                document.Add(dateText);

                // Table - FIXED ORDER: Registration Number FIRST
                var table = new PdfPTable(columnCount);
                table.WidthPercentage = 100;

                // Set column widths dynamically - FIXED ORDER
                var widths = new List<float>();
                if (columns.RegdNumber) widths.Add(1.5f);
                if (columns.StudentName) widths.Add(2.5f);
                if (columns.Email) widths.Add(3f);
                if (columns.Year) widths.Add(1f);
                if (columns.Department) widths.Add(1.5f);
                if (columns.EnrollmentTime) widths.Add(2.5f);
                
                table.SetWidths(widths.ToArray());

                // Headers - FIXED ORDER: Registration Number FIRST
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9);
                var headerColor = new BaseColor(111, 66, 193);
                
                if (columns.RegdNumber)
                    table.AddCell(new PdfPCell(new Phrase("Registration No", headerFont)) 
                    { BackgroundColor = headerColor, Padding = 5, HorizontalAlignment = Element.ALIGN_CENTER });
                if (columns.StudentName)
                    table.AddCell(new PdfPCell(new Phrase("Student Name", headerFont)) 
                    { BackgroundColor = headerColor, Padding = 5, HorizontalAlignment = Element.ALIGN_CENTER });
                if (columns.Email)
                    table.AddCell(new PdfPCell(new Phrase("Email", headerFont)) 
                    { BackgroundColor = headerColor, Padding = 5, HorizontalAlignment = Element.ALIGN_CENTER });
                if (columns.Year)
                    table.AddCell(new PdfPCell(new Phrase("Year", headerFont)) 
                    { BackgroundColor = headerColor, Padding = 5, HorizontalAlignment = Element.ALIGN_CENTER });
                if (columns.Department)
                    table.AddCell(new PdfPCell(new Phrase("Department", headerFont)) 
                    { BackgroundColor = headerColor, Padding = 5, HorizontalAlignment = Element.ALIGN_CENTER });
                if (columns.EnrollmentTime)
                    table.AddCell(new PdfPCell(new Phrase("Enrollment Time", headerFont)) 
                    { BackgroundColor = headerColor, Padding = 5, HorizontalAlignment = Element.ALIGN_CENTER });

                // Data - FIXED ORDER: Registration Number FIRST
                var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 8);
                foreach (var item in reportData)
                {
                    if (columns.RegdNumber)
                        table.AddCell(new PdfPCell(new Phrase(item.StudentRegdNumber ?? "", cellFont)) { Padding = 3 });
                    if (columns.StudentName)
                        table.AddCell(new PdfPCell(new Phrase(item.StudentName ?? "", cellFont)) { Padding = 3 });
                    if (columns.Email)
                        table.AddCell(new PdfPCell(new Phrase(item.StudentEmail ?? "", cellFont)) { Padding = 3 });
                    if (columns.Year)
                        table.AddCell(new PdfPCell(new Phrase(item.StudentYear ?? "", cellFont)) { Padding = 3 });
                    if (columns.Department)
                        table.AddCell(new PdfPCell(new Phrase(item.StudentYear ?? "", cellFont)) { Padding = 3 });
                    if (columns.EnrollmentTime)
                    {
                        // Convert UTC to local time and format with milliseconds
                        var localTime = item.EnrolledAt.ToLocalTime();
                        var timeStr = localTime.ToString("MM/dd/yyyy hh:mm:ss.fff tt");
                        table.AddCell(new PdfPCell(new Phrase(timeStr, cellFont)) { Padding = 3 });
                    }
                }

                document.Add(table);
                
                // Summary
                var summaryFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);
                var summary = new Paragraph($"\nTotal Students Enrolled: {reportData.Count}", summaryFont);
                summary.Alignment = Element.ALIGN_CENTER;
                summary.SpacingBefore = 20;
                document.Add(summary);
                
                document.Close();

                var fileName = $"Students_Enrolled_{request.SubjectName?.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                var content = stream.ToArray();

                return File(content, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PDF export error: {ex}");
                return StatusCode(500, $"Error exporting to PDF: {ex.Message}");
            }
        }
    }

    // Request models for faculty reports
    public class FacultyReportRequest
    {
        public string SubjectName { get; set; }
    }

    public class FacultyExportRequest
    {
        public string SubjectName { get; set; }
        public List<EnrollmentReportDto> ReportData { get; set; }
        public FacultyColumnSelection SelectedColumns { get; set; }
    }

    public class FacultyColumnSelection
    {
        public bool RegdNumber { get; set; } = true;
        public bool StudentName { get; set; } = true;
        public bool Email { get; set; } = true;
        public bool Year { get; set; } = true;
        public bool Department { get; set; } = true;
        public bool EnrollmentTime { get; set; } = true;
    }
}
