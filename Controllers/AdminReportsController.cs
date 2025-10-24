using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorLiveMentor.Models;
using OfficeOpenXml;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace TutorLiveMentor.Controllers
{
    public class AdminReportsController : Controller
    {
        private readonly AppDbContext _context;

        public AdminReportsController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Helper method to check if department is CSEDS
        /// </summary>
        private bool IsCSEDSDepartment(string department)
        {
            if (string.IsNullOrEmpty(department)) return false;
            var normalizedDept = department.ToUpper().Replace("(", "").Replace(")", "").Replace(" ", "").Replace("-", "").Trim();
            return normalizedDept == "CSEDS";
        }

        /// <summary>
        /// Get faculty members who teach a specific subject
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetFacultyBySubject(int subjectId)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department))
                return Unauthorized();

            try
            {
                var faculty = await _context.AssignedSubjects
                    .Include(a => a.Faculty)
                    .Include(a => a.Subject)
                    .Where(a => a.SubjectId == subjectId && 
                              (a.Subject.Department == "CSEDS" || a.Subject.Department == "CSE(DS)"))
                    .Select(a => new { 
                        FacultyId = a.Faculty.FacultyId, 
                        Name = a.Faculty.Name,
                        Email = a.Faculty.Email
                    })
                    .Distinct()
                    .OrderBy(f => f.Name)
                    .ToListAsync();

                return Json(new { success = true, data = faculty });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error loading faculty: {ex.Message}" });
            }
        }

        /// <summary>
        /// Generate CSEDS enrollment report with filters
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GenerateCSEDSReport([FromBody] CSEDSReportsViewModel filters)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department))
                return Unauthorized();

            try
            {
                // Start with base query for CSEDS enrollments
                var query = _context.StudentEnrollments
                    .Include(se => se.Student)
                    .Include(se => se.AssignedSubject)
                        .ThenInclude(a => a.Subject)
                    .Include(se => se.AssignedSubject)
                        .ThenInclude(a => a.Faculty)
                    .Where(se => se.Student.Department == "CSEDS" || se.Student.Department == "CSE(DS)");

                // Debug: Log the base query count
                var totalEnrollments = await query.CountAsync();
                Console.WriteLine($"Total CSEDS enrollments found: {totalEnrollments}");

                // Apply filters step by step
                if (filters.SelectedSubjectId.HasValue)
                {
                    query = query.Where(se => se.AssignedSubject.SubjectId == filters.SelectedSubjectId.Value);
                    var afterSubjectFilter = await query.CountAsync();
                    Console.WriteLine($"After subject filter ({filters.SelectedSubjectId}): {afterSubjectFilter} records");
                }

                if (filters.SelectedFacultyId.HasValue)
                {
                    query = query.Where(se => se.AssignedSubject.FacultyId == filters.SelectedFacultyId.Value);
                    var afterFacultyFilter = await query.CountAsync();
                    Console.WriteLine($"After faculty filter ({filters.SelectedFacultyId}): {afterFacultyFilter} records");
                }

                if (filters.SelectedYear.HasValue)
                {
                    query = query.Where(se => se.AssignedSubject.Subject.Year == filters.SelectedYear.Value);
                    var afterYearFilter = await query.CountAsync();
                    Console.WriteLine($"After year filter ({filters.SelectedYear}): {afterYearFilter} records");
                }

                if (!string.IsNullOrEmpty(filters.SelectedSemester))
                {
                    query = query.Where(se => se.AssignedSubject.Subject.Semester == filters.SelectedSemester);
                    var afterSemesterFilter = await query.CountAsync();
                    Console.WriteLine($"After semester filter ({filters.SelectedSemester}): {afterSemesterFilter} records");
                }

                // Execute query and project to DTO
                var results = await query
                    .OrderBy(se => se.Student.FullName)
                    .ThenBy(se => se.AssignedSubject.Subject.Name)
                    .Select(se => new EnrollmentReportDto
                    {
                        StudentName = se.Student.FullName,
                        StudentRegdNumber = se.Student.RegdNumber, // Added registration number
                        StudentEmail = se.Student.Email,
                        StudentYear = se.Student.Year,
                        SubjectName = se.AssignedSubject.Subject.Name,
                        FacultyName = se.AssignedSubject.Faculty.Name,
                        FacultyEmail = se.AssignedSubject.Faculty.Email,
                        EnrollmentDate = DateTime.Now, // You might want to add actual enrollment date to your model
                        Semester = se.AssignedSubject.Subject.Semester ?? ""
                    })
                    .ToListAsync();

                Console.WriteLine($"Final query returned {results.Count} results");

                // If no results found, let's return some debug information
                if (results.Count == 0)
                {
                    // Get all available data for debugging
                    var debugInfo = new
                    {
                        TotalStudents = await _context.Students.Where(s => s.Department == "CSEDS" || s.Department == "CSE(DS)").CountAsync(),
                        TotalEnrollments = await _context.StudentEnrollments.Include(se => se.Student).Where(se => se.Student.Department == "CSEDS" || se.Student.Department == "CSE(DS)").CountAsync(),
                        AvailableSubjects = await _context.Subjects.Where(s => s.Department == "CSEDS" || s.Department == "CSE(DS)").Select(s => new { s.SubjectId, s.Name, s.Year, s.Semester }).ToListAsync(),
                        AvailableFaculty = await _context.Faculties.Where(f => f.Department == "CSEDS" || f.Department == "CSE(DS)").Select(f => new { f.FacultyId, f.Name }).ToListAsync(),
                        AppliedFilters = new
                        {
                            filters.SelectedSubjectId,
                            filters.SelectedFacultyId,
                            filters.SelectedYear,
                            filters.SelectedSemester
                        }
                    };

                    return Json(new { 
                        success = true, 
                        data = results,
                        debug = debugInfo,
                        message = "No enrollments found with the selected criteria. Check the debug information." 
                    });
                }

                return Json(new { success = true, data = results });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GenerateCSEDSReport: {ex}");
                return Json(new { success = false, message = $"Error generating report: {ex.Message}" });
            }
        }

        /// <summary>
        /// Export CSEDS report to Excel
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ExportCSEDSReportExcel([FromForm] string filters)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department))
                return Unauthorized();

            try
            {
                // Parse filters
                CSEDSReportsViewModel filterObj;
                try
                {
                    filterObj = System.Text.Json.JsonSerializer.Deserialize<CSEDSReportsViewModel>(filters);
                }
                catch (Exception ex)
                {
                    return BadRequest($"Invalid filter data: {ex.Message}");
                }
                
                // Get data using the same logic as GenerateCSEDSReport
                var query = _context.StudentEnrollments
                    .Include(se => se.Student)
                    .Include(se => se.AssignedSubject)
                        .ThenInclude(a => a.Subject)
                    .Include(se => se.AssignedSubject)
                        .ThenInclude(a => a.Faculty)
                    .Where(se => se.Student.Department == "CSEDS" || se.Student.Department == "CSE(DS)");

                // Apply filters
                if (filterObj.SelectedSubjectId.HasValue)
                    query = query.Where(se => se.AssignedSubject.SubjectId == filterObj.SelectedSubjectId.Value);

                if (filterObj.SelectedFacultyId.HasValue)
                    query = query.Where(se => se.AssignedSubject.FacultyId == filterObj.SelectedFacultyId.Value);

                if (filterObj.SelectedYear.HasValue)
                    query = query.Where(se => se.AssignedSubject.Subject.Year == filterObj.SelectedYear.Value);

                if (!string.IsNullOrEmpty(filterObj.SelectedSemester))
                    query = query.Where(se => se.AssignedSubject.Subject.Semester == filterObj.SelectedSemester);

                var data = await query
                    .OrderBy(se => se.Student.FullName)
                    .ThenBy(se => se.AssignedSubject.Subject.Name)
                    .Select(se => new
                    {
                        StudentName = se.Student.FullName,
                        StudentRegdNumber = se.Student.RegdNumber,
                        StudentEmail = se.Student.Email,
                        StudentYear = se.Student.Year,
                        SubjectName = se.AssignedSubject.Subject.Name,
                        FacultyName = se.AssignedSubject.Faculty.Name,
                        FacultyEmail = se.AssignedSubject.Faculty.Email,
                        Semester = se.AssignedSubject.Subject.Semester ?? ""
                    })
                    .ToListAsync();

                if (data.Count == 0)
                {
                    return BadRequest("No data found for the selected criteria");
                }

                // Create Excel file
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("CSEDS Enrollment Report");

                // Headers
                worksheet.Cells[1, 1].Value = "Student Name";
                worksheet.Cells[1, 2].Value = "Registration No";
                worksheet.Cells[1, 3].Value = "Student Email";
                worksheet.Cells[1, 4].Value = "Student Year";
                worksheet.Cells[1, 5].Value = "Subject Name";
                worksheet.Cells[1, 6].Value = "Faculty Name";
                worksheet.Cells[1, 7].Value = "Faculty Email";
                worksheet.Cells[1, 8].Value = "Semester";

                // Header styling
                using (var range = worksheet.Cells[1, 1, 1, 8])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                }

                // Data
                for (int i = 0; i < data.Count; i++)
                {
                    var row = i + 2;
                    var item = data[i];
                    
                    worksheet.Cells[row, 1].Value = item.StudentName;
                    worksheet.Cells[row, 2].Value = item.StudentRegdNumber;
                    worksheet.Cells[row, 3].Value = item.StudentEmail;
                    worksheet.Cells[row, 4].Value = item.StudentYear;
                    worksheet.Cells[row, 5].Value = item.SubjectName;
                    worksheet.Cells[row, 6].Value = item.FacultyName;
                    worksheet.Cells[row, 7].Value = item.FacultyEmail;
                    worksheet.Cells[row, 8].Value = item.Semester;
                }

                // Auto-fit columns
                worksheet.Cells.AutoFitColumns();

                // Generate file
                var fileName = $"CSEDS_Enrollment_Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var content = package.GetAsByteArray();

                // Set proper response headers for download
                Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
                Response.Headers["Cache-Control"] = "no-cache";
                Response.Headers["Pragma"] = "no-cache";
                Response.Headers["Expires"] = "0";

                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Excel export error: {ex}");
                return StatusCode(500, $"Error exporting to Excel: {ex.Message}");
            }
        }

        /// <summary>
        /// Export CSEDS report to PDF
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ExportCSEDSReportPDF([FromForm] string filters)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department))
                return Unauthorized();

            try
            {
                // Parse filters
                CSEDSReportsViewModel filterObj;
                try
                {
                    filterObj = System.Text.Json.JsonSerializer.Deserialize<CSEDSReportsViewModel>(filters);
                }
                catch (Exception ex)
                {
                    return BadRequest($"Invalid filter data: {ex.Message}");
                }
                
                // Get data using the same logic as GenerateCSEDSReport
                var query = _context.StudentEnrollments
                    .Include(se => se.Student)
                    .Include(se => se.AssignedSubject)
                        .ThenInclude(a => a.Subject)
                    .Include(se => se.AssignedSubject)
                        .ThenInclude(a => a.Faculty)
                    .Where(se => se.Student.Department == "CSEDS" || se.Student.Department == "CSE(DS)");

                // Apply filters
                if (filterObj.SelectedSubjectId.HasValue)
                    query = query.Where(se => se.AssignedSubject.SubjectId == filterObj.SelectedSubjectId.Value);

                if (filterObj.SelectedFacultyId.HasValue)
                    query = query.Where(se => se.AssignedSubject.FacultyId == filterObj.SelectedFacultyId.Value);

                if (filterObj.SelectedYear.HasValue)
                    query = query.Where(se => se.AssignedSubject.Subject.Year == filterObj.SelectedYear.Value);

                if (!string.IsNullOrEmpty(filterObj.SelectedSemester))
                    query = query.Where(se => se.AssignedSubject.Subject.Semester == filterObj.SelectedSemester);

                var data = await query
                    .OrderBy(se => se.Student.FullName)
                    .ThenBy(se => se.AssignedSubject.Subject.Name)
                    .Select(se => new
                    {
                        StudentName = se.Student.FullName,
                        StudentRegdNumber = se.Student.RegdNumber,
                        StudentEmail = se.Student.Email,
                        StudentYear = se.Student.Year,
                        SubjectName = se.AssignedSubject.Subject.Name,
                        FacultyName = se.AssignedSubject.Faculty.Name,
                        FacultyEmail = se.AssignedSubject.Faculty.Email,
                        Semester = se.AssignedSubject.Subject.Semester ?? ""
                    })
                    .ToListAsync();

                if (data.Count == 0)
                {
                    return BadRequest("No data found for the selected criteria");
                }

                // Create PDF using iTextSharp
                using var stream = new MemoryStream();
                var document = new Document(PageSize.A4.Rotate(), 25, 25, 30, 30);
                var writer = PdfWriter.GetInstance(document, stream);
                
                document.Open();
                
                // Title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                var title = new Paragraph("CSEDS Department - Enrollment Report", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                title.SpacingAfter = 20;
                document.Add(title);

                // Date
                var dateFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                var dateText = new Paragraph($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", dateFont);
                dateText.Alignment = Element.ALIGN_RIGHT;
                dateText.SpacingAfter = 20;
                document.Add(dateText);

                // Table
                var table = new PdfPTable(8);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 2, 1.5f, 2.5f, 1, 2, 2, 2.5f, 1 });

                // Headers
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
                table.AddCell(new PdfPCell(new Phrase("Student Name", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase("Reg No", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase("Student Email", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase("Year", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase("Subject", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase("Faculty", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase("Faculty Email", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase("Semester", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });

                // Data
                var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);
                foreach (var item in data)
                {
                    table.AddCell(new PdfPCell(new Phrase(item.StudentName ?? "", cellFont)) { Padding = 3 });
                    table.AddCell(new PdfPCell(new Phrase(item.StudentRegdNumber ?? "", cellFont)) { Padding = 3 });
                    table.AddCell(new PdfPCell(new Phrase(item.StudentEmail ?? "", cellFont)) { Padding = 3 });
                    table.AddCell(new PdfPCell(new Phrase(item.StudentYear ?? "", cellFont)) { Padding = 3 });
                    table.AddCell(new PdfPCell(new Phrase(item.SubjectName ?? "", cellFont)) { Padding = 3 });
                    table.AddCell(new PdfPCell(new Phrase(item.FacultyName ?? "", cellFont)) { Padding = 3 });
                    table.AddCell(new PdfPCell(new Phrase(item.FacultyEmail ?? "", cellFont)) { Padding = 3 });
                    table.AddCell(new PdfPCell(new Phrase(item.Semester ?? "", cellFont)) { Padding = 3 });
                }

                document.Add(table);
                
                // Summary
                var summaryFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);
                var summary = new Paragraph($"\nTotal Records: {data.Count}", summaryFont);
                summary.Alignment = Element.ALIGN_CENTER;
                summary.SpacingBefore = 20;
                document.Add(summary);
                
                document.Close();

                var fileName = $"CSEDS_Enrollment_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                var content = stream.ToArray();

                // Set proper response headers for download
                Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
                Response.Headers["Cache-Control"] = "no-cache";
                Response.Headers["Pragma"] = "no-cache";
                Response.Headers["Expires"] = "0";

                return File(content, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PDF export error: {ex}");
                return StatusCode(500, $"Error exporting to PDF: {ex.Message}");
            }
        }

        /// <summary>
        /// Debug endpoint to check database data
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DebugDatabaseData()
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department))
                return Unauthorized();

            try
            {
                var debugData = new
                {
                    TotalStudents = await _context.Students.CountAsync(),
                    CSEDSStudents = await _context.Students.Where(s => s.Department == "CSEDS" || s.Department == "CSE(DS)").CountAsync(),
                    TotalEnrollments = await _context.StudentEnrollments.CountAsync(),
                    CSEDSEnrollments = await _context.StudentEnrollments
                        .Include(se => se.Student)
                        .Where(se => se.Student.Department == "CSEDS" || se.Student.Department == "CSE(DS)")
                        .CountAsync(),
                    TotalSubjects = await _context.Subjects.CountAsync(),
                    CSEDSSubjects = await _context.Subjects.Where(s => s.Department == "CSEDS" || s.Department == "CSE(DS)").CountAsync(),
                    TotalFaculty = await _context.Faculties.CountAsync(),
                    CSEDSFaculty = await _context.Faculties.Where(f => f.Department == "CSEDS" || f.Department == "CSE(DS)").CountAsync(),
                    TotalAssignedSubjects = await _context.AssignedSubjects.CountAsync(),
                    CSEDSAssignedSubjects = await _context.AssignedSubjects
                        .Include(a => a.Subject)
                        .Where(a => a.Subject.Department == "CSEDS" || a.Subject.Department == "CSE(DS)")
                        .CountAsync(),
                    
                    // Sample data
                    SampleStudents = await _context.Students
                        .Where(s => s.Department == "CSEDS" || s.Department == "CSE(DS)")
                        .Take(3)
                        .Select(s => new { s.FullName, s.RegdNumber, s.Department, s.Year, s.Email })
                        .ToListAsync(),
                    
                    SampleEnrollments = await _context.StudentEnrollments
                        .Include(se => se.Student)
                        .Include(se => se.AssignedSubject)
                            .ThenInclude(a => a.Subject)
                        .Include(se => se.AssignedSubject)
                            .ThenInclude(a => a.Faculty)
                        .Where(se => se.Student.Department == "CSEDS" || se.Student.Department == "CSE(DS)")
                        .Take(3)
                        .Select(se => new { 
                            StudentName = se.Student.FullName,
                            StudentRegdNumber = se.Student.RegdNumber,
                            StudentDepartment = se.Student.Department,
                            SubjectName = se.AssignedSubject.Subject.Name,
                            FacultyName = se.AssignedSubject.Faculty.Name,
                            SubjectYear = se.AssignedSubject.Subject.Year,
                            SubjectSemester = se.AssignedSubject.Subject.Semester
                        })
                        .ToListAsync()
                };

                return Json(debugData);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        /// <summary>
        /// Test endpoint for export functionality
        /// </summary>
        [HttpGet]
        public IActionResult TestExportFunctionality()
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department))
                return Unauthorized();

            try
            {
                // Test Excel package
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Test");
                worksheet.Cells[1, 1].Value = "Test Excel Export";
                var excelContent = package.GetAsByteArray();

                // Test PDF creation
                using var stream = new MemoryStream();
                var document = new Document();
                var writer = PdfWriter.GetInstance(document, stream);
                document.Open();
                document.Add(new Paragraph("Test PDF Export"));
                document.Close();
                var pdfContent = stream.ToArray();

                return Json(new { 
                    success = true, 
                    message = "Export functionality test passed",
                    excelSize = excelContent.Length,
                    pdfSize = pdfContent.Length 
                });
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = $"Export functionality test failed: {ex.Message}",
                    stackTrace = ex.StackTrace 
                });
            }
        }

        /// <summary>
        /// Simple Excel export using current report data
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ExportCurrentReportExcel([FromBody] List<EnrollmentReportDto> reportData)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department))
                return Unauthorized();

            try
            {
                if (reportData == null || reportData.Count == 0)
                {
                    return BadRequest("No data to export");
                }

                // Create Excel file
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("CSEDS Enrollment Report");

                // Headers
                worksheet.Cells[1, 1].Value = "Student Name";
                worksheet.Cells[1, 2].Value = "Registration No";
                worksheet.Cells[1, 3].Value = "Student Email";
                worksheet.Cells[1, 4].Value = "Student Year";
                worksheet.Cells[1, 5].Value = "Subject Name";
                worksheet.Cells[1, 6].Value = "Faculty Name";
                worksheet.Cells[1, 7].Value = "Faculty Email";
                worksheet.Cells[1, 8].Value = "Semester";

                // Header styling
                using (var range = worksheet.Cells[1, 1, 1, 8])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                }

                // Data
                for (int i = 0; i < reportData.Count; i++)
                {
                    var row = i + 2;
                    var item = reportData[i];
                    
                    worksheet.Cells[row, 1].Value = item.StudentName;
                    worksheet.Cells[row, 2].Value = item.StudentRegdNumber;
                    worksheet.Cells[row, 3].Value = item.StudentEmail;
                    worksheet.Cells[row, 4].Value = item.StudentYear;
                    worksheet.Cells[row, 5].Value = item.SubjectName;
                    worksheet.Cells[row, 6].Value = item.FacultyName;
                    worksheet.Cells[row, 7].Value = item.FacultyEmail;
                    worksheet.Cells[row, 8].Value = item.Semester;
                }

                // Auto-fit columns
                worksheet.Cells.AutoFitColumns();

                // Generate file
                var fileName = $"CSEDS_Enrollment_Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
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
        /// Simple PDF export using current report data
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ExportCurrentReportPDF([FromBody] List<EnrollmentReportDto> reportData)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department))
                return Unauthorized();

            try
            {
                if (reportData == null || reportData.Count == 0)
                {
                    return BadRequest("No data to export");
                }

                // Create PDF using iTextSharp
                using var stream = new MemoryStream();
                var document = new Document(PageSize.A4.Rotate(), 25, 25, 30, 30);
                var writer = PdfWriter.GetInstance(document, stream);
                
                document.Open();
                
                // Title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                var title = new Paragraph("CSEDS Department - Enrollment Report", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                title.SpacingAfter = 20;
                document.Add(title);

                // Date
                var dateFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                var dateText = new Paragraph($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", dateFont);
                dateText.Alignment = Element.ALIGN_RIGHT;
                dateText.SpacingAfter = 20;
                document.Add(dateText);

                // Table
                var table = new PdfPTable(8);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 2, 1.5f, 2.5f, 1, 2, 2, 2.5f, 1 });

                // Headers
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
                table.AddCell(new PdfPCell(new Phrase("Student Name", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase("Reg No", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase("Student Email", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase("Year", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase("Subject", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase("Faculty", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase("Faculty Email", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase("Semester", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });

                // Data
                var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);
                foreach (var item in reportData)
                {
                    table.AddCell(new PdfPCell(new Phrase(item.StudentName ?? "", cellFont)) { Padding = 3 });
                    table.AddCell(new PdfPCell(new Phrase(item.StudentRegdNumber ?? "", cellFont)) { Padding = 3 });
                    table.AddCell(new PdfPCell(new Phrase(item.StudentEmail ?? "", cellFont)) { Padding = 3 });
                    table.AddCell(new PdfPCell(new Phrase(item.StudentYear ?? "", cellFont)) { Padding = 3 });
                    table.AddCell(new PdfPCell(new Phrase(item.SubjectName ?? "", cellFont)) { Padding = 3 });
                    table.AddCell(new PdfPCell(new Phrase(item.FacultyName ?? "", cellFont)) { Padding = 3 });
                    table.AddCell(new PdfPCell(new Phrase(item.FacultyEmail ?? "", cellFont)) { Padding = 3 });
                    table.AddCell(new PdfPCell(new Phrase(item.Semester ?? "", cellFont)) { Padding = 3 });
                }

                document.Add(table);
                
                // Summary
                var summaryFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);
                var summary = new Paragraph($"\nTotal Records: {reportData.Count}", summaryFont);
                summary.Alignment = Element.ALIGN_CENTER;
                summary.SpacingBefore = 20;
                document.Add(summary);
                
                document.Close();

                var fileName = $"CSEDS_Enrollment_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                var content = stream.ToArray();

                return File(content, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PDF export error: {ex}");
                return StatusCode(500, $"Error exporting to PDF: {ex.Message}");
            }
        }

        /// <summary>
        /// Export students to Excel
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ExportStudentsExcel([FromBody] List<StudentDetailDto> studentsData)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department))
                return Unauthorized();

            try
            {
                if (studentsData == null || studentsData.Count == 0)
                {
                    return BadRequest("No student data to export");
                }

                // Create Excel file
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("CSEDS Students");

                // Headers
                worksheet.Cells[1, 1].Value = "Student ID";
                worksheet.Cells[1, 2].Value = "Full Name";
                worksheet.Cells[1, 3].Value = "Registration Number";
                worksheet.Cells[1, 4].Value = "Email";
                worksheet.Cells[1, 5].Value = "Year";
                worksheet.Cells[1, 6].Value = "Department";
                worksheet.Cells[1, 7].Value = "Total Enrollments";
                worksheet.Cells[1, 8].Value = "Enrolled Subjects";

                // Header styling
                using (var range = worksheet.Cells[1, 1, 1, 8])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                }

                // Data
                for (int i = 0; i < studentsData.Count; i++)
                {
                    var row = i + 2;
                    var student = studentsData[i];
                    
                    worksheet.Cells[row, 1].Value = student.StudentId;
                    worksheet.Cells[row, 2].Value = student.FullName;
                    worksheet.Cells[row, 3].Value = student.RegdNumber;
                    worksheet.Cells[row, 4].Value = student.Email;
                    worksheet.Cells[row, 5].Value = student.Year;
                    worksheet.Cells[row, 6].Value = student.Department;
                    worksheet.Cells[row, 7].Value = student.TotalEnrollments;
                    
                    // Enrolled subjects as comma-separated list
                    var subjects = student.EnrolledSubjects?.Select(s => $"{s.SubjectName} (Sem {s.Semester})").ToList() ?? new List<string>();
                    worksheet.Cells[row, 8].Value = string.Join(", ", subjects);
                }

                // Auto-fit columns
                worksheet.Cells.AutoFitColumns();

                // Generate file
                var fileName = $"CSEDS_Students_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var content = package.GetAsByteArray();

                // Set proper response headers for download
                Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
                Response.Headers["Cache-Control"] = "no-cache";
                Response.Headers["Pragma"] = "no-cache";
                Response.Headers["Expires"] = "0";

                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Excel export error: {ex}");
                return StatusCode(500, $"Error exporting to Excel: {ex.Message}");
            }
        }

        /// <summary>
        /// Export students to PDF
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ExportStudentsPDF([FromBody] List<StudentDetailDto> studentsData)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department))
                return Unauthorized();

            try
            {
                if (studentsData == null || studentsData.Count == 0)
                {
                    return BadRequest("No student data to export");
                }

                // Create PDF using iTextSharp
                using var stream = new MemoryStream();
                var document = new Document(PageSize.A4.Rotate(), 25, 25, 30, 30);
                var writer = PdfWriter.GetInstance(document, stream);
                
                document.Open();
                
                // Title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                var title = new Paragraph("CSEDS Department - Students Report", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                title.SpacingAfter = 20;
                document.Add(title);

                // Date
                var dateFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                var dateText = new Paragraph($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", dateFont);
                dateText.Alignment = Element.ALIGN_RIGHT;
                dateText.SpacingAfter = 20;
                document.Add(dateText);

                // Table
                var table = new PdfPTable(7);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 1.5f, 2.5f, 1.5f, 2.5f, 1f, 1f, 3f });

                // Headers
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
                table.AddCell(new PdfPCell(new Phrase("Student ID", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase("Full Name", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase("Reg No", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase("Email", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase("Year", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase("Enrollments", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });
                table.AddCell(new PdfPCell(new Phrase("Enrolled Subjects", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, Padding = 5 });

                // Data
                var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 8);
                foreach (var student in studentsData)
                {
                    table.AddCell(new PdfPCell(new Phrase(student.StudentId ?? "", cellFont)) { Padding = 3 });
                    table.AddCell(new PdfPCell(new Phrase(student.FullName ?? "", cellFont)) { Padding = 3 });
                    table.AddCell(new PdfPCell(new Phrase(student.RegdNumber ?? "", cellFont)) { Padding = 3 });
                    table.AddCell(new PdfPCell(new Phrase(student.Email ?? "", cellFont)) { Padding = 3 });
                    table.AddCell(new PdfPCell(new Phrase(student.Year ?? "", cellFont)) { Padding = 3 });
                    table.AddCell(new PdfPCell(new Phrase(student.TotalEnrollments.ToString(), cellFont)) { Padding = 3 });
                    
                    // Enrolled subjects
                    var subjects = student.EnrolledSubjects?.Select(s => $"{s.SubjectName} (Sem {s.Semester})").ToList() ?? new List<string>();
                    var subjectsText = string.Join(", ", subjects.Take(3)); // Limit to 3 subjects for PDF
                    if (subjects.Count > 3) subjectsText += $" +{subjects.Count - 3} more";
                    table.AddCell(new PdfPCell(new Phrase(subjectsText, cellFont)) { Padding = 3 });
                }

                document.Add(table);
                
                // Summary
                var summaryFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);
                var summary = new Paragraph($"\nTotal Students: {studentsData.Count}", summaryFont);
                summary.Alignment = Element.ALIGN_CENTER;
                summary.SpacingBefore = 20;
                document.Add(summary);
                
                document.Close();

                var fileName = $"CSEDS_Students_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
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
}