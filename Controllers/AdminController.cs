using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorLiveMentor.Models;
using TutorLiveMentor.Services;
using System.Linq;
using OfficeOpenXml;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.Text;

namespace TutorLiveMentor.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly SignalRService _signalRService;

        public AdminController(AppDbContext context, SignalRService signalRService)
        {
            _context = context;
            _signalRService = signalRService;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(AdminLoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                // Find admin with matching credentials in database
                var admin = await _context.Admins
                    .FirstOrDefaultAsync(a => a.Email == model.Email && a.Password == model.Password);

                if (admin == null)
                {
                    ModelState.AddModelError("", "Invalid admin credentials!");
                    return View(model);
                }

                // Clear any existing session
                HttpContext.Session.Clear();

                // Store admin information in session
                HttpContext.Session.SetInt32("AdminId", admin.AdminId);
                HttpContext.Session.SetString("AdminEmail", admin.Email);
                HttpContext.Session.SetString("AdminDepartment", admin.Department);

                // Force session to be saved immediately
                await HttpContext.Session.CommitAsync();

                // Notify system of admin login
                await _signalRService.NotifyUserActivity(admin.Email, "Admin", "Logged In", $"{admin.Department} department administrator logged into the system");

                // Redirect to department-specific dashboard based on department
                if (admin.Department.ToUpper() == "CSEDS")
                {
                    return RedirectToAction("CSEDSDashboard");
                }
                else
                {
                    return RedirectToAction("MainDashboard");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Login error: {ex.Message}");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult MainDashboard()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");

            if (adminId == null)
            {
                TempData["ErrorMessage"] = "Please login to access the admin dashboard.";
                return RedirectToAction("Login");
            }

            ViewBag.AdminId = adminId;
            ViewBag.AdminEmail = HttpContext.Session.GetString("AdminEmail");
            ViewBag.AdminDepartment = HttpContext.Session.GetString("AdminDepartment");

            return View();
        }

        /// <summary>
        /// CSEDS Department-specific dashboard with filtered data and management capabilities
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CSEDSDashboard()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            var department = HttpContext.Session.GetString("AdminDepartment");

            if (adminId == null || department?.ToUpper() != "CSEDS")
            {
                TempData["ErrorMessage"] = "Access denied. CSEDS department access only.";
                return RedirectToAction("Login");
            }

            // Get CSEDS-specific statistics
            var viewModel = new CSEDSDashboardViewModel
            {
                AdminEmail = HttpContext.Session.GetString("AdminEmail") ?? "",
                AdminDepartment = department ?? "",

                // Count only CSEDS department data
                CSEDSStudentsCount = await _context.Students
                    .CountAsync(s => s.Department.ToUpper() == "CSEDS"),

                CSEDSFacultyCount = await _context.Faculties
                    .CountAsync(f => f.Department.ToUpper() == "CSEDS"),

                CSEDSSubjectsCount = await _context.Subjects
                    .CountAsync(s => s.Department.ToUpper() == "CSEDS"),

                CSEDSEnrollmentsCount = await _context.StudentEnrollments
                    .Include(se => se.Student)
                    .CountAsync(se => se.Student.Department.ToUpper() == "CSEDS"),

                // Get recent CSEDS students
                RecentStudents = await _context.Students
                    .Where(s => s.Department.ToUpper() == "CSEDS")
                    .OrderByDescending(s => s.Id)
                    .Take(5)
                    .Select(s => new StudentActivityDto
                    {
                        FullName = s.FullName,
                        Email = s.Email,
                        Department = s.Department,
                        Year = s.Year
                    })
                    .ToListAsync(),

                // Get recent CSEDS enrollments
                RecentEnrollments = await _context.StudentEnrollments
                    .Include(se => se.Student)
                    .Include(se => se.AssignedSubject)
                        .ThenInclude(a => a.Subject)
                    .Include(se => se.AssignedSubject)
                        .ThenInclude(a => a.Faculty)
                    .Where(se => se.Student.Department.ToUpper() == "CSEDS")
                    .OrderByDescending(se => se.StudentEnrollmentId)
                    .Take(10)
                    .Select(se => new EnrollmentActivityDto
                    {
                        StudentName = se.Student.FullName,
                        SubjectName = se.AssignedSubject.Subject.Name,
                        FacultyName = se.AssignedSubject.Faculty.Name,
                        EnrollmentDate = DateTime.Now // Note: Add this field to StudentEnrollment if needed
                    })
                    .ToListAsync()
            };

            return View(viewModel);
        }

        /// <summary>
        /// Get CSEDS department system information via AJAX
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CSEDSSystemInfo()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            var department = HttpContext.Session.GetString("AdminDepartment");

            if (adminId == null || department?.ToUpper() != "CSEDS")
                return Unauthorized();

            // Get CSEDS-specific system information
            var systemInfo = new
            {
                DatabaseStats = new
                {
                    StudentsCount = await _context.Students
                        .CountAsync(s => s.Department.ToUpper() == "CSEDS"),
                    FacultiesCount = await _context.Faculties
                        .CountAsync(f => f.Department.ToUpper() == "CSEDS"),
                    SubjectsCount = await _context.Subjects
                        .CountAsync(s => s.Department.ToUpper() == "CSEDS"),
                    EnrollmentsCount = await _context.StudentEnrollments
                        .Include(se => se.Student)
                        .CountAsync(se => se.Student.Department.ToUpper() == "CSEDS")
                },
                RecentActivity = new
                {
                    RecentStudents = await _context.Students
                        .Where(s => s.Department.ToUpper() == "CSEDS")
                        .OrderByDescending(s => s.Id)
                        .Take(5)
                        .Select(s => new { s.FullName, s.Email, s.Department, s.Year })
                        .ToListAsync(),
                    RecentEnrollments = await _context.StudentEnrollments
                        .Include(se => se.Student)
                        .Include(se => se.AssignedSubject)
                            .ThenInclude(a => a.Subject)
                        .Include(se => se.AssignedSubject)
                            .ThenInclude(a => a.Faculty)
                        .Where(se => se.Student.Department.ToUpper() == "CSEDS")
                        .OrderByDescending(se => se.StudentEnrollmentId)
                        .Take(10)
                        .Select(se => new
                        {
                            StudentName = se.Student.FullName,
                            SubjectName = se.AssignedSubject.Subject.Name,
                            FacultyName = se.AssignedSubject.Faculty.Name
                        })
                        .ToListAsync()
                }
            };

            return Json(systemInfo);
        }

        /// <summary>
        /// Manage CSEDS faculty members
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ManageCSEDSFaculty()
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (department?.ToUpper() != "CSEDS")
                return RedirectToAction("Login");

            var faculty = await _context.Faculties
                .Where(f => f.Department.ToUpper() == "CSEDS")
                .OrderBy(f => f.Name)
                .ToListAsync();

            return View(faculty);
        }

        /// <summary>
        /// Add new CSEDS faculty member
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddCSEDSFaculty([FromBody] CSEDSFacultyViewModel model)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (department?.ToUpper() != "CSEDS")
                return Unauthorized();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Check if faculty email already exists
            var existingFaculty = await _context.Faculties
                .FirstOrDefaultAsync(f => f.Email == model.Email);
            if (existingFaculty != null)
                return BadRequest("Faculty with this email already exists");

            var faculty = new Faculty
            {
                Name = model.Name,
                Email = model.Email,
                Password = model.Password,
                Department = "CSEDS"
            };

            _context.Faculties.Add(faculty);
            await _context.SaveChangesAsync();

            // Assign faculty to selected subjects
            if (model.SelectedSubjectIds.Any())
            {
                foreach (var subjectId in model.SelectedSubjectIds)
                {
                    var assignedSubject = new AssignedSubject
                    {
                        FacultyId = faculty.FacultyId,
                        SubjectId = subjectId,
                        Department = "CSEDS",
                        Year = 1, // Default year, can be updated later
                        SelectedCount = 0
                    };
                    _context.AssignedSubjects.Add(assignedSubject);
                }
                await _context.SaveChangesAsync();
            }

            await _signalRService.NotifyUserActivity(
                HttpContext.Session.GetString("AdminEmail") ?? "",
                "Admin",
                "Faculty Added",
                $"New CSEDS faculty member {faculty.Name} added to the system"
            );

            return Ok(new { success = true, message = "Faculty added successfully" });
        }

        /// <summary>
        /// Update existing CSEDS faculty member
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdateCSEDSFaculty([FromBody] CSEDSFacultyViewModel model)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (department?.ToUpper() != "CSEDS")
                return Unauthorized();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var faculty = await _context.Faculties
                .FirstOrDefaultAsync(f => f.FacultyId == model.FacultyId && f.Department.ToUpper() == "CSEDS");

            if (faculty == null)
                return NotFound();

            faculty.Name = model.Name;
            faculty.Email = model.Email;
            if (!string.IsNullOrEmpty(model.Password))
                faculty.Password = model.Password;

            await _context.SaveChangesAsync();

            await _signalRService.NotifyUserActivity(
                HttpContext.Session.GetString("AdminEmail") ?? "",
                "Admin",
                "Faculty Updated",
                $"CSEDS faculty member {faculty.Name} information updated"
            );

            return Ok(new { success = true, message = "Faculty updated successfully" });
        }

        /// <summary>
        /// Manage CSEDS subjects
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ManageCSEDSSubjects()
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (department?.ToUpper() != "CSEDS")
                return RedirectToAction("Login");

            var subjects = await _context.Subjects
                .Where(s => s.Department.ToUpper() == "CSEDS")
                .OrderBy(s => s.Year)
                .ThenBy(s => s.Name)
                .ToListAsync();

            return View(subjects);
        }

        /// <summary>
        /// Add new CSEDS subject
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddCSEDSSubject([FromBody] CSEDSSubjectViewModel model)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (department?.ToUpper() != "CSEDS")
                return Unauthorized();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Validate semester dates
            if (model.SemesterEndDate <= model.SemesterStartDate)
                return BadRequest("Semester end date must be after start date");

            var subject = new Subject
            {
                Name = model.Name,
                Department = "CSEDS",
                Year = model.Year,
                Semester = model.Semester,
                SemesterStartDate = model.SemesterStartDate,
                SemesterEndDate = model.SemesterEndDate
            };

            _context.Subjects.Add(subject);
            await _context.SaveChangesAsync();

            await _signalRService.NotifyUserActivity(
                HttpContext.Session.GetString("AdminEmail") ?? "",
                "Admin",
                "Subject Added",
                $"New CSEDS subject {subject.Name} added for Year {subject.Year}, {subject.Semester} semester"
            );

            return Ok(new { success = true, message = "Subject added successfully" });
        }

        /// <summary>
        /// Get CSEDS reports and analytics
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CSEDSReports()
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (department?.ToUpper() != "CSEDS")
                return RedirectToAction("Login");

            var viewModel = new CSEDSReportsViewModel
            {
                AvailableSubjects = await _context.Subjects
                    .Where(s => s.Department.ToUpper() == "CSEDS")
                    .OrderBy(s => s.Name)
                    .ToListAsync(),

                AvailableFaculty = await _context.Faculties
                    .Where(f => f.Department.ToUpper() == "CSEDS")
                    .OrderBy(f => f.Name)
                    .ToListAsync()
            };

            return View(viewModel);
        }

        /// <summary>
        /// Generate CSEDS report data based on filters
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GenerateCSEDSReport([FromBody] CSEDSReportsViewModel filters)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (department?.ToUpper() != "CSEDS")
                return Unauthorized();

            var query = _context.StudentEnrollments
                .Include(se => se.Student)
                .Include(se => se.AssignedSubject)
                    .ThenInclude(a => a.Subject)
                .Include(se => se.AssignedSubject)
                    .ThenInclude(a => a.Faculty)
                .Where(se => se.Student.Department.ToUpper() == "CSEDS");

            // Apply filters
            if (filters.SelectedSubjectId.HasValue)
                query = query.Where(se => se.AssignedSubject.SubjectId == filters.SelectedSubjectId.Value);

            if (filters.SelectedFacultyId.HasValue)
                query = query.Where(se => se.AssignedSubject.FacultyId == filters.SelectedFacultyId.Value);

            if (filters.SelectedYear.HasValue)
                query = query.Where(se => se.Student.Year == filters.SelectedYear.Value.ToString());

            if (!string.IsNullOrEmpty(filters.SelectedSemester))
                query = query.Where(se => se.AssignedSubject.Subject.Semester == filters.SelectedSemester);

            // Note: Date filtering would require adding enrollment date to StudentEnrollment model

            var results = await query
                .Select(se => new EnrollmentReportDto
                {
                    StudentName = se.Student.FullName,
                    StudentEmail = se.Student.Email,
                    StudentYear = se.Student.Year,
                    SubjectName = se.AssignedSubject.Subject.Name,
                    FacultyName = se.AssignedSubject.Faculty.Name,
                    FacultyEmail = se.AssignedSubject.Faculty.Email,
                    EnrollmentDate = DateTime.Now, // Add proper date field
                    Semester = se.AssignedSubject.Subject.Semester ?? ""
                })
                .ToListAsync();

            return Json(new { success = true, data = results, totalRecords = results.Count });
        }

        /// <summary>
        /// Export CSEDS report data to Excel
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ExportCSEDSReportExcel(string filters)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (department?.ToUpper() != "CSEDS")
                return Unauthorized();

            // Parse filters from form data
            var filterObj = new CSEDSReportsViewModel();
            if (!string.IsNullOrEmpty(filters))
            {
                try
                {
                    filterObj = System.Text.Json.JsonSerializer.Deserialize<CSEDSReportsViewModel>(filters);
                }
                catch
                {
                    // Use default empty filters
                }
            }

            // Get filtered data (reuse the same logic as GenerateCSEDSReport)
            var reportData = await GetFilteredCSEDSReportData(filterObj);

            // Create Excel file
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("CSEDS Report");

            // Add headers
            worksheet.Cells[1, 1].Value = "Student Name";
            worksheet.Cells[1, 2].Value = "Student Email";
            worksheet.Cells[1, 3].Value = "Year";
            worksheet.Cells[1, 4].Value = "Subject";
            worksheet.Cells[1, 5].Value = "Faculty";
            worksheet.Cells[1, 6].Value = "Faculty Email";
            worksheet.Cells[1, 7].Value = "Semester";
            worksheet.Cells[1, 8].Value = "Enrollment Date";

            // Style headers
            using var range = worksheet.Cells[1, 1, 1, 8];
            range.Style.Font.Bold = true;
            range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);

            // Add data
            for (int i = 0; i < reportData.Count; i++)
            {
                var row = i + 2;
                var data = reportData[i];
                worksheet.Cells[row, 1].Value = data.StudentName;
                worksheet.Cells[row, 2].Value = data.StudentEmail;
                worksheet.Cells[row, 3].Value = data.StudentYear;
                worksheet.Cells[row, 4].Value = data.SubjectName;
                worksheet.Cells[row, 5].Value = data.FacultyName;
                worksheet.Cells[row, 6].Value = data.FacultyEmail;
                worksheet.Cells[row, 7].Value = data.Semester;
                worksheet.Cells[row, 8].Value = data.EnrollmentDate.ToString("yyyy-MM-dd");
            }

            worksheet.Cells.AutoFitColumns();

            var content = package.GetAsByteArray();
            var fileName = $"CSEDS_Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        /// <summary>
        /// Export CSEDS report data to PDF
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ExportCSEDSReportPDF(string filters)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (department?.ToUpper() != "CSEDS")
                return Unauthorized();

            // Parse filters from form data
            var filterObj = new CSEDSReportsViewModel();
            if (!string.IsNullOrEmpty(filters))
            {
                try
                {
                    filterObj = System.Text.Json.JsonSerializer.Deserialize<CSEDSReportsViewModel>(filters);
                }
                catch
                {
                    // Use default empty filters
                }
            }

            // Get filtered data
            var reportData = await GetFilteredCSEDSReportData(filterObj);

            using var stream = new MemoryStream();
            var document = new Document(PageSize.A4.Rotate());
            PdfWriter.GetInstance(document, stream);

            document.Open();

            // Add title
            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
            var title = new Paragraph("CSEDS Department Report", titleFont)
            {
                Alignment = Element.ALIGN_CENTER
            };
            document.Add(title);
            document.Add(new Paragraph(" ")); // Space

            // Create table
            var table = new PdfPTable(8);
            table.WidthPercentage = 100;

            // Add headers
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
            table.AddCell(new PdfPCell(new Phrase("Student Name", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Email", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Year", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Subject", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Faculty", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Faculty Email", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Semester", headerFont)));
            table.AddCell(new PdfPCell(new Phrase("Date", headerFont)));

            // Add data
            var dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);
            foreach (var data in reportData)
            {
                table.AddCell(new PdfPCell(new Phrase(data.StudentName, dataFont)));
                table.AddCell(new PdfPCell(new Phrase(data.StudentEmail, dataFont)));
                table.AddCell(new PdfPCell(new Phrase(data.StudentYear, dataFont)));
                table.AddCell(new PdfPCell(new Phrase(data.SubjectName, dataFont)));
                table.AddCell(new PdfPCell(new Phrase(data.FacultyName, dataFont)));
                table.AddCell(new PdfPCell(new Phrase(data.FacultyEmail, dataFont)));
                table.AddCell(new PdfPCell(new Phrase(data.Semester, dataFont)));
                table.AddCell(new PdfPCell(new Phrase(data.EnrollmentDate.ToString("yyyy-MM-dd"), dataFont)));
            }

            document.Add(table);
            document.Close();

            var content = stream.ToArray();
            var fileName = $"CSEDS_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

            return File(content, "application/pdf", fileName);
        }

        /// <summary>
        /// Helper method to get filtered report data
        /// </summary>
        private async Task<List<EnrollmentReportDto>> GetFilteredCSEDSReportData(CSEDSReportsViewModel filters)
        {
            var query = _context.StudentEnrollments
                .Include(se => se.Student)
                .Include(se => se.AssignedSubject)
                    .ThenInclude(a => a.Subject)
                .Include(se => se.AssignedSubject)
                    .ThenInclude(a => a.Faculty)
                .Where(se => se.Student.Department.ToUpper() == "CSEDS");

            // Apply filters (same logic as GenerateCSEDSReport)
            if (filters.SelectedSubjectId.HasValue)
                query = query.Where(se => se.AssignedSubject.SubjectId == filters.SelectedSubjectId.Value);

            if (filters.SelectedFacultyId.HasValue)
                query = query.Where(se => se.AssignedSubject.FacultyId == filters.SelectedFacultyId.Value);

            if (filters.SelectedYear.HasValue)
                query = query.Where(se => se.Student.Year == filters.SelectedYear.Value.ToString());

            if (!string.IsNullOrEmpty(filters.SelectedSemester))
                query = query.Where(se => se.AssignedSubject.Subject.Semester == filters.SelectedSemester);

            return await query
                .Select(se => new EnrollmentReportDto
                {
                    StudentName = se.Student.FullName,
                    StudentEmail = se.Student.Email,
                    StudentYear = se.Student.Year,
                    SubjectName = se.AssignedSubject.Subject.Name,
                    FacultyName = se.AssignedSubject.Faculty.Name,
                    FacultyEmail = se.AssignedSubject.Faculty.Email,
                    EnrollmentDate = DateTime.Now, // Add proper date field
                    Semester = se.AssignedSubject.Subject.Semester ?? ""
                })
                .ToListAsync();
        }

        // Keep the old CSEDashboard method for backward compatibility, but redirect to CSEDS
        [HttpGet]
        public IActionResult CSEDashboard()
        {
            return RedirectToAction("CSEDSDashboard");
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null)
                return RedirectToAction("Login");

            // Get system statistics for admin dashboard
            var stats = new AdminDashboardViewModel
            {
                TotalStudents = await _context.Students.CountAsync(),
                TotalFaculties = await _context.Faculties.CountAsync(),
                TotalSubjects = await _context.Subjects.CountAsync(),
                TotalEnrollments = await _context.StudentEnrollments.CountAsync(),
                TotalAdmins = await _context.Admins.CountAsync()
            };

            return View(stats);
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null)
                return RedirectToAction("Login");

            var admin = await _context.Admins.FindAsync(adminId.Value);
            if (admin == null)
            {
                return NotFound();
            }

            return View(admin);
        }

        [HttpGet]
        public async Task<IActionResult> ManageAdmins()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null)
                return RedirectToAction("Login");

            var admins = await _context.Admins.OrderBy(a => a.Email).ToListAsync();
            return View(admins);
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId != null)
            {
                var admin = await _context.Admins.FindAsync(adminId.Value);
                if (admin != null)
                {
                    // Notify system of admin logout
                    await _signalRService.NotifyUserActivity(admin.Email, "Admin", "Logged Out", $"{admin.Department} department administrator logged out of the system");
                }
            }

            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        [HttpGet]
        public async Task<IActionResult> SystemInfo()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null)
                return RedirectToAction("Login");

            // Get detailed system information
            var systemInfo = new
            {
                DatabaseStats = new
                {
                    StudentsCount = await _context.Students.CountAsync(),
                    FacultiesCount = await _context.Faculties.CountAsync(),
                    SubjectsCount = await _context.Subjects.CountAsync(),
                    EnrollmentsCount = await _context.StudentEnrollments.CountAsync(),
                    AdminsCount = await _context.Admins.CountAsync()
                },
                RecentActivity = new
                {
                    RecentStudents = await _context.Students
                        .OrderByDescending(s => s.Id)
                        .Take(5)
                        .Select(s => new { s.FullName, s.Email, s.Department, s.Year })
                        .ToListAsync(),
                    RecentEnrollments = await _context.StudentEnrollments
                        .Include(se => se.Student)
                        .Include(se => se.AssignedSubject)
                            .ThenInclude(a => a.Subject)
                        .Include(se => se.AssignedSubject)
                            .ThenInclude(a => a.Faculty)
                        .OrderByDescending(se => se.StudentEnrollmentId)
                        .Take(10)
                        .Select(se => new
                        {
                            StudentName = se.Student.FullName,
                            SubjectName = se.AssignedSubject.Subject.Name,
                            FacultyName = se.AssignedSubject.Faculty.Name
                        })
                        .ToListAsync()
                }
            };

            return Json(systemInfo);
        }
    }

    /// <summary>
    /// View model for general admin dashboard statistics
    /// </summary>
    public class AdminDashboardViewModel
    {
        public int TotalStudents { get; set; }
        public int TotalFaculties { get; set; }
        public int TotalSubjects { get; set; }
        public int TotalEnrollments { get; set; }
        public int TotalAdmins { get; set; }
    }
}