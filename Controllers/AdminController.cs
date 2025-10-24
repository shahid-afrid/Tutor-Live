using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorLiveMentor.Models;
using TutorLiveMentor.Services;
using System.Linq;
using OfficeOpenXml;
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

        /// <summary>
        /// Helper method to check if department is CSEDS (handles specific variations only)
        /// This method is for in-memory use only, not for LINQ queries
        /// </summary>
        private bool IsCSEDSDepartment(string department)
        {
            if (string.IsNullOrEmpty(department)) return false;

            // Normalize the department string
            var normalizedDept = department.ToUpper().Replace("(", "").Replace(")", "").Replace(" ", "").Replace("-", "").Trim();

            // Only match specific CSEDS variations: "CSEDS" and "CSE(DS)"
            return normalizedDept == "CSEDS" || normalizedDept == "CSEDS"; // CSE(DS) becomes CSEDS after normalization
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

                // Update last login time
                admin.LastLogin = DateTime.Now;
                await _context.SaveChangesAsync();

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
                if (IsCSEDSDepartment(admin.Department))
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
        /// CSEDS Department-specific dashboard with enhanced faculty and subject management
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CSEDSDashboard()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            var department = HttpContext.Session.GetString("AdminDepartment");

            if (adminId == null || !IsCSEDSDepartment(department))
            {
                TempData["ErrorMessage"] = "Access denied. CSEDS department access only.";
                return RedirectToAction("Login");
            }

            // Get comprehensive CSEDS data - only match "CSEDS" and "CSE(DS)"
            var viewModel = new CSEDSDashboardViewModel
            {
                AdminEmail = HttpContext.Session.GetString("AdminEmail") ?? "",
                AdminDepartment = department ?? "",

                // Count only CSEDS department data - exact matches only
                CSEDSStudentsCount = await _context.Students
                    .Where(s => s.Department == "CSEDS" || s.Department == "CSE(DS)")
                    .CountAsync(),

                CSEDSFacultyCount = await _context.Faculties
                    .Where(f => f.Department == "CSEDS" || f.Department == "CSE(DS)")
                    .CountAsync(),

                CSEDSSubjectsCount = await _context.Subjects
                    .Where(s => s.Department == "CSEDS" || s.Department == "CSE(DS)")
                    .CountAsync(),

                CSEDSEnrollmentsCount = await _context.StudentEnrollments
                    .Include(se => se.Student)
                    .Where(se => se.Student.Department == "CSEDS" || se.Student.Department == "CSE(DS)")
                    .CountAsync(),

                // Get recent CSEDS students
                RecentStudents = await _context.Students
                    .Where(s => s.Department == "CSEDS" || s.Department == "CSE(DS)")
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
                    .Where(se => se.Student.Department == "CSEDS" || se.Student.Department == "CSE(DS)")
                    .OrderByDescending(se => se.StudentEnrollmentId)
                    .Take(10)
                    .Select(se => new EnrollmentActivityDto
                    {
                        StudentName = se.Student.FullName,
                        SubjectName = se.AssignedSubject.Subject.Name,
                        FacultyName = se.AssignedSubject.Faculty.Name,
                        EnrollmentDate = DateTime.Now
                    })
                    .ToListAsync(),

                // Get all department faculty for management
                DepartmentFaculty = await _context.Faculties
                    .Where(f => f.Department == "CSEDS" || f.Department == "CSE(DS)")
                    .OrderBy(f => f.Name)
                    .ToListAsync(),

                // Get all department subjects for management
                DepartmentSubjects = await _context.Subjects
                    .Where(s => s.Department == "CSEDS" || s.Department == "CSE(DS)")
                    .OrderBy(s => s.Year)
                    .ThenBy(s => s.Name)
                    .ToListAsync(),

                // Get subject-faculty mappings
                SubjectFacultyMappings = await GetSubjectFacultyMappings()
            };

            return View(viewModel);
        }

        /// <summary>
        /// Get comprehensive faculty management view
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ManageCSEDSFaculty()
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department))
                return RedirectToAction("Login");

            var viewModel = new FacultyManagementViewModel
            {
                DepartmentFaculty = await GetFacultyWithAssignments(),
                AvailableSubjects = await _context.Subjects
                    .Where(s => s.Department == "CSEDS" || s.Department == "CSE(DS)")
                    .OrderBy(s => s.Year)
                    .ThenBy(s => s.Name)
                    .ToListAsync()
            };

            return View(viewModel);
        }

        /// <summary>
        /// Get subject-faculty assignment management view
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ManageSubjectAssignments()
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department))
                return RedirectToAction("Login");

            var viewModel = new SubjectManagementViewModel
            {
                DepartmentSubjects = await GetSubjectsWithAssignments(),
                AvailableFaculty = await _context.Faculties
                    .Where(f => f.Department == "CSEDS" || f.Department == "CSE(DS)")
                    .OrderBy(f => f.Name)
                    .ToListAsync()
            };

            return View(viewModel);
        }

        /// <summary>
        /// Assign faculty to subject
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AssignFacultyToSubject([FromBody] FacultySubjectAssignmentRequest request)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department))
                return Unauthorized();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                // Verify subject belongs to CSEDS department
                var subject = await _context.Subjects
                    .FirstOrDefaultAsync(s => s.SubjectId == request.SubjectId &&
                                            (s.Department == "CSEDS" || s.Department == "CSE(DS)"));

                if (subject == null)
                    return BadRequest("Subject not found or does not belong to CSEDS department");

                // Verify faculty belongs to CSEDS department
                var faculty = await _context.Faculties
                    .Where(f => request.FacultyIds.Contains(f.FacultyId) &&
                              (f.Department == "CSEDS" || f.Department == "CSE(DS)"))
                    .ToListAsync();

                if (faculty.Count != request.FacultyIds.Count)
                    return BadRequest("One or more faculty members not found or do not belong to CSEDS department");

                // Remove existing assignments for this subject
                var existingAssignments = await _context.AssignedSubjects
                    .Where(a => a.SubjectId == request.SubjectId)
                    .ToListAsync();

                _context.AssignedSubjects.RemoveRange(existingAssignments);

                // Create new assignments
                foreach (var facultyId in request.FacultyIds)
                {
                    var assignedSubject = new AssignedSubject
                    {
                        FacultyId = facultyId,
                        SubjectId = request.SubjectId,
                        Department = "CSEDS",
                        Year = subject.Year,
                        SelectedCount = 0
                    };
                    _context.AssignedSubjects.Add(assignedSubject);
                }

                await _context.SaveChangesAsync();

                await _signalRService.NotifyUserActivity(
                    HttpContext.Session.GetString("AdminEmail") ?? "",
                    "Admin",
                    "Faculty Assignment Updated",
                    $"Faculty assignments updated for subject: {subject.Name}"
                );

                return Ok(new { success = true, message = "Faculty assignments updated successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = $"Error updating assignments: {ex.Message}" });
            }
        }

        /// <summary>
        /// Remove faculty assignment from subject
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RemoveFacultyAssignment([FromBody] RemoveFacultyAssignmentRequest request)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department))
                return Unauthorized();

            try
            {
                var assignment = await _context.AssignedSubjects
                    .Include(a => a.Subject)
                    .Include(a => a.Faculty)
                    .FirstOrDefaultAsync(a => a.AssignedSubjectId == request.AssignedSubjectId &&
                                           (a.Subject.Department == "CSEDS" || a.Subject.Department == "CSE(DS)"));

                if (assignment == null)
                    return NotFound("Assignment not found");

                // Check if there are active enrollments
                var hasEnrollments = await _context.StudentEnrollments
                    .AnyAsync(se => se.AssignedSubjectId == request.AssignedSubjectId);

                if (hasEnrollments)
                    return BadRequest(new { success = false, message = "Cannot remove assignment with active student enrollments" });

                _context.AssignedSubjects.Remove(assignment);
                await _context.SaveChangesAsync();

                await _signalRService.NotifyUserActivity(
                    HttpContext.Session.GetString("AdminEmail") ?? "",
                    "Admin",
                    "Faculty Assignment Removed",
                    $"Faculty {assignment.Faculty.Name} unassigned from subject: {assignment.Subject.Name}"
                );

                return Ok(new { success = true, message = "Faculty assignment removed successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = $"Error removing assignment: {ex.Message}" });
            }
        }

        /// <summary>
        /// Get all faculty in department with their assignments
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetDepartmentFaculty()
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department))
                return Unauthorized();

            var faculty = await GetFacultyWithAssignments();
            return Json(new { success = true, data = faculty });
        }

        /// <summary>
        /// Get all subjects in department with their assignments
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetDepartmentSubjects()
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department))
                return Unauthorized();

            var subjects = await GetSubjectsWithAssignments();
            return Json(new { success = true, data = subjects });
        }

        /// <summary>
        /// Get available faculty for a specific subject
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAvailableFacultyForSubject(int subjectId)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department))
                return Unauthorized();

            // Get all CSEDS faculty
            var allFaculty = await _context.Faculties
                .Where(f => f.Department == "CSEDS" || f.Department == "CSE(DS)")
                .Select(f => new { f.FacultyId, f.Name, f.Email })
                .ToListAsync();

            // Get currently assigned faculty for this subject
            var assignedFaculty = await _context.AssignedSubjects
                .Where(a => a.SubjectId == subjectId)
                .Select(a => a.FacultyId)
                .ToListAsync();

            var result = allFaculty.Select(f => new
            {
                f.FacultyId,
                f.Name,
                f.Email,
                IsAssigned = assignedFaculty.Contains(f.FacultyId)
            }).ToList();

            return Json(new { success = true, data = result });
        }

        /// <summary>
        /// Helper method to get faculty with their assignments
        /// </summary>
        private async Task<List<FacultyDetailDto>> GetFacultyWithAssignments()
        {
            // First get all CSEDS faculty
            var faculty = await _context.Faculties
                .Where(f => f.Department == "CSEDS" || f.Department == "CSE(DS)")
                .ToListAsync();

            var result = new List<FacultyDetailDto>();

            foreach (var f in faculty)
            {
                var assignedSubjects = await _context.AssignedSubjects
                    .Include(a => a.Subject)
                    .Where(a => a.FacultyId == f.FacultyId &&
                              (a.Subject.Department == "CSEDS" || a.Subject.Department == "CSE(DS)"))
                    .ToListAsync();

                var enrollmentCount = 0;
                var subjectInfos = new List<AssignedSubjectInfo>();

                foreach (var assignment in assignedSubjects)
                {
                    var enrollments = await _context.StudentEnrollments
                        .CountAsync(se => se.AssignedSubjectId == assignment.AssignedSubjectId);

                    enrollmentCount += enrollments;

                    subjectInfos.Add(new AssignedSubjectInfo
                    {
                        AssignedSubjectId = assignment.AssignedSubjectId,
                        SubjectId = assignment.SubjectId,
                        SubjectName = assignment.Subject.Name,
                        Year = assignment.Subject.Year,
                        Semester = assignment.Subject.Semester ?? "",
                        EnrollmentCount = enrollments
                    });
                }

                result.Add(new FacultyDetailDto
                {
                    FacultyId = f.FacultyId,
                    Name = f.Name,
                    Email = f.Email,
                    Department = f.Department,
                    AssignedSubjects = subjectInfos,
                    TotalEnrollments = enrollmentCount
                });
            }

            return result.OrderBy(f => f.Name).ToList();
        }

        /// <summary>
        /// Helper method to get subjects with their assignments
        /// </summary>
        private async Task<List<SubjectDetailDto>> GetSubjectsWithAssignments()
        {
            // First get all CSEDS subjects
            var subjects = await _context.Subjects
                .Where(s => s.Department == "CSEDS" || s.Department == "CSE(DS)")
                .ToListAsync();

            var result = new List<SubjectDetailDto>();

            foreach (var s in subjects)
            {
                var assignedFaculty = await _context.AssignedSubjects
                    .Include(a => a.Faculty)
                    .Where(a => a.SubjectId == s.SubjectId &&
                              (a.Faculty.Department == "CSEDS" || a.Faculty.Department == "CSE(DS)"))
                    .ToListAsync();

                var totalEnrollments = 0;
                var facultyInfos = new List<FacultyInfo>();

                foreach (var assignment in assignedFaculty)
                {
                    var enrollments = await _context.StudentEnrollments
                        .CountAsync(se => se.AssignedSubjectId == assignment.AssignedSubjectId);

                    totalEnrollments += enrollments;

                    facultyInfos.Add(new FacultyInfo
                    {
                        FacultyId = assignment.FacultyId,
                        Name = assignment.Faculty.Name,
                        Email = assignment.Faculty.Email,
                        AssignedSubjectId = assignment.AssignedSubjectId
                    });
                }

                result.Add(new SubjectDetailDto
                {
                    SubjectId = s.SubjectId,
                    Name = s.Name,
                    Department = s.Department,
                    Year = s.Year,
                    Semester = s.Semester ?? "",
                    SemesterStartDate = s.SemesterStartDate,
                    SemesterEndDate = s.SemesterEndDate,
                    AssignedFaculty = facultyInfos,
                    TotalEnrollments = totalEnrollments,
                    IsActive = s.SemesterEndDate == null || s.SemesterEndDate >= DateTime.Now
                });
            }

            return result.OrderBy(s => s.Year).ThenBy(s => s.Name).ToList();
        }

        /// <summary>
        /// Helper method to get subject-faculty mappings
        /// </summary>
        private async Task<List<SubjectFacultyMappingDto>> GetSubjectFacultyMappings()
        {
            // First get all CSEDS subjects
            var subjects = await _context.Subjects
                .Where(s => s.Department == "CSEDS" || s.Department == "CSE(DS)")
                .ToListAsync();

            var result = new List<SubjectFacultyMappingDto>();

            foreach (var s in subjects)
            {
                var assignedFaculty = await _context.AssignedSubjects
                    .Include(a => a.Faculty)
                    .Where(a => a.SubjectId == s.SubjectId &&
                              (a.Faculty.Department == "CSEDS" || a.Faculty.Department == "CSE(DS)"))
                    .ToListAsync();

                var enrollmentCount = 0;
                var facultyInfos = new List<FacultyInfo>();

                foreach (var assignment in assignedFaculty)
                {
                    var enrollments = await _context.StudentEnrollments
                        .CountAsync(se => se.AssignedSubjectId == assignment.AssignedSubjectId);

                    enrollmentCount += enrollments;

                    facultyInfos.Add(new FacultyInfo
                    {
                        FacultyId = assignment.FacultyId,
                        Name = assignment.Faculty.Name,
                        Email = assignment.Faculty.Email,
                        AssignedSubjectId = assignment.AssignedSubjectId
                    });
                }

                result.Add(new SubjectFacultyMappingDto
                {
                    SubjectId = s.SubjectId,
                    SubjectName = s.Name,
                    Year = s.Year,
                    Semester = s.Semester ?? "",
                    SemesterStartDate = s.SemesterStartDate,
                    SemesterEndDate = s.SemesterEndDate,
                    AssignedFaculty = facultyInfos,
                    EnrollmentCount = enrollmentCount
                });
            }

            return result.OrderBy(s => s.Year).ThenBy(s => s.SubjectName).ToList();
        }

        /// <summary>
        /// Get CSEDS department system information via AJAX
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CSEDSSystemInfo()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            var department = HttpContext.Session.GetString("AdminDepartment");

            if (adminId == null || !IsCSEDSDepartment(department))
                return Unauthorized();

            var systemInfo = new
            {
                DatabaseStats = new
                {
                    StudentsCount = await _context.Students
                        .Where(s => s.Department == "CSEDS" || s.Department == "CSE(DS)")
                        .CountAsync(),
                    FacultiesCount = await _context.Faculties
                        .Where(f => f.Department == "CSEDS" || f.Department == "CSE(DS)")
                        .CountAsync(),
                    SubjectsCount = await _context.Subjects
                        .Where(s => s.Department == "CSEDS" || s.Department == "CSE(DS)")
                        .CountAsync(),
                    EnrollmentsCount = await _context.StudentEnrollments
                        .Include(se => se.Student)
                        .Where(se => se.Student.Department == "CSEDS" || se.Student.Department == "CSE(DS)")
                        .CountAsync()
                },
                RecentActivity = new
                {
                    RecentStudents = await _context.Students
                        .Where(s => s.Department == "CSEDS" || s.Department == "CSE(DS)")
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
                        .Where(se => se.Student.Department == "CSEDS" || se.Student.Department == "CSE(DS)")
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

        [HttpPost]
        public async Task<IActionResult> AddCSEDSFaculty([FromBody] CSEDSFacultyViewModel model)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department))
                return Unauthorized();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var existingFaculty = await _context.Faculties.FirstOrDefaultAsync(f => f.Email == model.Email);
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

            if (model.SelectedSubjectIds.Any())
            {
                foreach (var subjectId in model.SelectedSubjectIds)
                {
                    var assignedSubject = new AssignedSubject
                    {
                        FacultyId = faculty.FacultyId,
                        SubjectId = subjectId,
                        Department = "CSEDS",
                        Year = 1,
                        SelectedCount = 0
                    };
                    _context.AssignedSubjects.Add(assignedSubject);
                }
                await _context.SaveChangesAsync();
            }

            await _signalRService.NotifyUserActivity(HttpContext.Session.GetString("AdminEmail") ?? "", "Admin", "Faculty Added", $"New CSEDS faculty member {faculty.Name} added to the system");

            return Ok(new { success = true, message = "Faculty added successfully" });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateCSEDSFaculty([FromBody] CSEDSFacultyViewModel model)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department))
                return Unauthorized();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var faculty = await _context.Faculties
                .FirstOrDefaultAsync(f => f.FacultyId == model.FacultyId &&
                                        (f.Department == "CSEDS" || f.Department == "CSE(DS)"));

            if (faculty == null)
                return NotFound();

            faculty.Name = model.Name;
            faculty.Email = model.Email;
            if (!string.IsNullOrEmpty(model.Password))
                faculty.Password = model.Password;

            await _context.SaveChangesAsync();
            await _signalRService.NotifyUserActivity(HttpContext.Session.GetString("AdminEmail") ?? "", "Admin", "Faculty Updated", $"CSEDS faculty member {faculty.Name} information updated");

            return Ok(new { success = true, message = "Faculty updated successfully" });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCSEDSFaculty([FromBody] dynamic data)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department))
                return Unauthorized();

            var facultyId = (int)data.facultyId;
            var faculty = await _context.Faculties
                .Include(f => f.AssignedSubjects)
                    .ThenInclude(a => a.Enrollments)
                .FirstOrDefaultAsync(f => f.FacultyId == facultyId &&
                                        (f.Department == "CSEDS" || f.Department == "CSE(DS)"));

            if (faculty == null)
                return NotFound();

            var hasEnrollments = faculty.AssignedSubjects?.Any(a => a.Enrollments?.Any() == true) == true;
            if (hasEnrollments)
                return BadRequest(new { success = false, message = "Cannot delete faculty with active student enrollments" });

            if (faculty.AssignedSubjects != null)
                _context.AssignedSubjects.RemoveRange(faculty.AssignedSubjects);

            _context.Faculties.Remove(faculty);
            await _context.SaveChangesAsync();
            await _signalRService.NotifyUserActivity(HttpContext.Session.GetString("AdminEmail") ?? "", "Admin", "Faculty Deleted", $"CSEDS faculty member {faculty.Name} has been deleted from the system");

            return Ok(new { success = true, message = "Faculty deleted successfully" });
        }

        [HttpGet]
        public async Task<IActionResult> ManageCSEDSSubjects()
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department))
                return RedirectToAction("Login");

            var subjects = await _context.Subjects
                .Where(s => s.Department == "CSEDS" || s.Department == "CSE(DS)")
                .OrderBy(s => s.Year)
                .ThenBy(s => s.Name)
                .ToListAsync();

            return View(subjects);
        }

        [HttpGet]
        public async Task<IActionResult> CSEDSReports()
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department))
                return RedirectToAction("Login");

            var viewModel = new CSEDSReportsViewModel
            {
                AvailableSubjects = await _context.Subjects
                    .Where(s => s.Department == "CSEDS" || s.Department == "CSE(DS)")
                    .OrderBy(s => s.Name)
                    .ToListAsync(),
                AvailableFaculty = await _context.Faculties
                    .Where(f => f.Department == "CSEDS" || f.Department == "CSE(DS)")
                    .OrderBy(f => f.Name)
                    .ToListAsync()
            };

            return View(viewModel);
        }

        // Keep the old CSEDashboard method for backward compatibility
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
                return NotFound();

            // Check if this is a CSEDS admin to return appropriate view
            if (IsCSEDSDepartment(admin.Department))
            {
                var csedsProfile = new AdminProfileViewModel
                {
                    AdminId = admin.AdminId,
                    Email = admin.Email,
                    Department = admin.Department,
                    CreatedDate = admin.CreatedDate,
                    LastLogin = admin.LastLogin
                };

                return View("CSEDSProfile", csedsProfile);
            }

            // For non-CSEDS admins, create a generic profile model
            var profileModel = new AdminProfileViewModel
            {
                AdminId = admin.AdminId,
                Email = admin.Email,
                Department = admin.Department,
                CreatedDate = admin.CreatedDate,
                LastLogin = admin.LastLogin
            };

            return View(profileModel);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(AdminProfileViewModel model)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null)
                return RedirectToAction("Login");

            var admin = await _context.Admins.FindAsync(adminId.Value);
            if (admin == null)
                return NotFound();

            if (!IsCSEDSDepartment(admin.Department))
                return Forbid();

            if (ModelState.IsValid)
            {
                try
                {
                    // Check if email already exists for another admin
                    var existingAdmin = await _context.Admins
                        .FirstOrDefaultAsync(a => a.Email == model.Email && a.AdminId != adminId);

                    if (existingAdmin != null)
                    {
                        ModelState.AddModelError("Email", "An admin with this email already exists.");
                        return View("CSEDSProfile", model);
                    }

                    admin.Email = model.Email;
                    admin.Department = model.Department;

                    await _context.SaveChangesAsync();

                    // Update session data
                    HttpContext.Session.SetString("AdminEmail", admin.Email);
                    HttpContext.Session.SetString("AdminDepartment", admin.Department);

                    TempData["SuccessMessage"] = "Profile updated successfully!";
                    return RedirectToAction("Profile");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error updating profile: {ex.Message}");
                }
            }

            return View("CSEDSProfile", model);
        }

        [HttpPost]
        public async Task<IActionResult> ChangeAdminPassword(AdminChangePasswordViewModel model)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null)
                return Json(new { success = false, message = "Please login to continue." });

            var admin = await _context.Admins.FindAsync(adminId.Value);
            if (admin == null)
                return Json(new { success = false, message = "Admin not found." });

            if (!IsCSEDSDepartment(admin.Department))
                return Json(new { success = false, message = "Unauthorized access." });

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = string.Join(", ", errors) });
            }

            try
            {
                // Verify current password
                if (admin.Password != model.CurrentPassword)
                {
                    return Json(new { success = false, message = "Current password is incorrect." });
                }

                // Update password
                admin.Password = model.NewPassword;
                await _context.SaveChangesAsync();

                await _signalRService.NotifyUserActivity(
                    admin.Email,
                    "Admin",
                    "Password Changed",
                    "CSEDS admin password was successfully changed"
                );

                return Json(new { success = true, message = "Password changed successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error changing password: {ex.Message}" });
            }
        }

        /// <summary>
        /// CSEDS Student Management
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ManageCSEDSStudents()
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department ?? ""))
                return RedirectToAction("AccessDenied");

            try
            {
                var viewModel = new StudentManagementViewModel
                {
                    AdminEmail = HttpContext.Session.GetString("AdminEmail") ?? "",
                    Department = "CSEDS"
                };

                // Get all CSEDS students with their enrollment details
                var students = await _context.Students
                    .Where(s => s.Department == "CSEDS" || s.Department == "CSE(DS)")
                    .Include(s => s.Enrollments)
                        .ThenInclude(e => e.AssignedSubject)
                            .ThenInclude(a => a.Subject)
                    .Include(s => s.Enrollments)
                        .ThenInclude(e => e.AssignedSubject)
                            .ThenInclude(a => a.Faculty)
                    .OrderBy(s => s.FullName)
                    .ToListAsync();

                viewModel.DepartmentStudents = students.Select(s => new StudentDetailDto
                {
                    StudentId = s.Id,
                    FullName = s.FullName,
                    RegdNumber = s.RegdNumber,
                    Email = s.Email,
                    Year = s.Year,
                    Department = s.Department,
                    TotalEnrollments = s.Enrollments?.Count ?? 0,
                    IsActive = true, // Assuming all students are active for now
                    EnrolledSubjects = s.Enrollments?.Select(e => new EnrolledSubjectInfo
                    {
                        EnrollmentId = e.StudentEnrollmentId,
                        SubjectId = e.AssignedSubject.SubjectId,
                        SubjectName = e.AssignedSubject.Subject.Name,
                        FacultyName = e.AssignedSubject.Faculty.Name,
                        Semester = e.AssignedSubject.Subject.Semester ?? "",
                        Year = e.AssignedSubject.Subject.Year,
                        IsActive = true
                    }).ToList() ?? new List<EnrolledSubjectInfo>()
                }).ToList();

                // Get available subjects for the department
                viewModel.AvailableSubjects = await _context.Subjects
                    .Where(s => s.Department == "CSEDS" || s.Department == "CSE(DS)")
                    .OrderBy(s => s.Year)
                    .ThenBy(s => s.Name)
                    .ToListAsync();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading student management: {ex.Message}";
                return RedirectToAction("CSEDSDashboard");
            }
        }

        /// <summary>
        /// Add new CSEDS student
        /// </summary>
        [HttpGet]
        public IActionResult AddCSEDSStudent()
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department ?? ""))
                return RedirectToAction("AccessDenied");

            var viewModel = new CSEDSStudentViewModel
            {
                Department = "CSEDS",
                IsEdit = false
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> AddCSEDSStudent(CSEDSStudentViewModel model)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department ?? ""))
                return RedirectToAction("AccessDenied");

            if (ModelState.IsValid)
            {
                try
                {
                    // Check if email or registration number already exists
                    var existingStudent = await _context.Students
                        .Where(s => s.Email == model.Email || s.RegdNumber == model.RegdNumber)
                        .FirstOrDefaultAsync();

                    if (existingStudent != null)
                    {
                        if (existingStudent.Email == model.Email)
                            ModelState.AddModelError("Email", "A student with this email already exists.");
                        if (existingStudent.RegdNumber == model.RegdNumber)
                            ModelState.AddModelError("RegdNumber", "A student with this registration number already exists.");

                        return View(model);
                    }

                    // Generate student ID
                    var studentId = model.RegdNumber; // Using registration number as ID

                    var student = new Student
                    {
                        Id = studentId,
                        FullName = model.FullName,
                        RegdNumber = model.RegdNumber,
                        Email = model.Email,
                        Password = string.IsNullOrEmpty(model.Password) ? "TutorLive123" : model.Password, // Default password
                        Year = model.Year,
                        Department = "CSEDS"
                    };

                    _context.Students.Add(student);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Student added successfully!";
                    return RedirectToAction("ManageCSEDSStudents");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error adding student: {ex.Message}");
                }
            }

            return View(model);
        }

        /// <summary>
        /// Edit CSEDS student
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> EditCSEDSStudent(string id)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department ?? ""))
                return RedirectToAction("AccessDenied");

            if (string.IsNullOrEmpty(id))
                return NotFound();

            try
            {
                var student = await _context.Students
                    .Where(s => s.Id == id && (s.Department == "CSEDS" || s.Department == "CSE(DS)"))
                    .FirstOrDefaultAsync();

                if (student == null)
                    return NotFound();

                var viewModel = new CSEDSStudentViewModel
                {
                    StudentId = student.Id,
                    FullName = student.FullName,
                    RegdNumber = student.RegdNumber,
                    Email = student.Email,
                    Year = student.Year,
                    Department = student.Department,
                    IsEdit = true
                };

                return View("AddCSEDSStudent", viewModel);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading student: {ex.Message}";
                return RedirectToAction("ManageCSEDSStudents");
            }
        }

        [HttpPost]
        public async Task<IActionResult> EditCSEDSStudent(CSEDSStudentViewModel model)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department ?? ""))
                return RedirectToAction("AccessDenied");

            if (ModelState.IsValid)
            {
                try
                {
                    var student = await _context.Students
                        .Where(s => s.Id == model.StudentId && (s.Department == "CSEDS" || s.Department == "CSE(DS)"))
                        .FirstOrDefaultAsync();

                    if (student == null)
                        return NotFound();

                    // Check if email or registration number already exists (excluding current student)
                    var existingStudent = await _context.Students
                        .Where(s => s.Id != model.StudentId && (s.Email == model.Email || s.RegdNumber == model.RegdNumber))
                        .FirstOrDefaultAsync();

                    if (existingStudent != null)
                    {
                        if (existingStudent.Email == model.Email)
                            ModelState.AddModelError("Email", "A student with this email already exists.");
                        if (existingStudent.RegdNumber == model.RegdNumber)
                            ModelState.AddModelError("RegdNumber", "A student with this registration number already exists.");

                        return View("AddCSEDSStudent", model);
                    }

                    // Update student details
                    student.FullName = model.FullName;
                    student.RegdNumber = model.RegdNumber;
                    student.Email = model.Email;
                    student.Year = model.Year;

                    if (!string.IsNullOrEmpty(model.Password))
                        student.Password = model.Password;

                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Student updated successfully!";
                    return RedirectToAction("ManageCSEDSStudents");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error updating student: {ex.Message}");
                }
            }

            return View("AddCSEDSStudent", model);
        }

        /// <summary>
        /// Delete CSEDS student
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> DeleteCSEDSStudent(string id)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department ?? ""))
                return Json(new { success = false, message = "Unauthorized access" });

            try
            {
                var student = await _context.Students
                    .Include(s => s.Enrollments)
                    .Where(s => s.Id == id && (s.Department == "CSEDS" || s.Department == "CSE(DS)"))
                    .FirstOrDefaultAsync();

                if (student == null)
                    return Json(new { success = false, message = "Student not found" });

                // Remove enrollments first
                if (student.Enrollments != null && student.Enrollments.Any())
                {
                    _context.StudentEnrollments.RemoveRange(student.Enrollments);
                }

                // Remove student
                _context.Students.Remove(student);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Student deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error deleting student: {ex.Message}" });
            }
        }

        /// <summary>
        /// Get filtered students for AJAX requests
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GetFilteredStudents([FromBody] StudentSearchFilter filter)
        {
            var department = HttpContext.Session.GetString("AdminDepartment");
            if (!IsCSEDSDepartment(department ?? ""))
                return Json(new { success = false, message = "Unauthorized access" });

            try
            {
                var query = _context.Students
                    .Include(s => s.Enrollments)
                        .ThenInclude(e => e.AssignedSubject)
                            .ThenInclude(a => a.Subject)
                    .Include(s => s.Enrollments)
                        .ThenInclude(e => e.AssignedSubject)
                            .ThenInclude(a => a.Faculty)
                    .Where(s => s.Department == "CSEDS" || s.Department == "CSE(DS)");

                // Apply filters
                if (!string.IsNullOrEmpty(filter.SearchText))
                {
                    query = query.Where(s => s.FullName.Contains(filter.SearchText) ||
                                           s.Email.Contains(filter.SearchText) ||
                                           s.RegdNumber.Contains(filter.SearchText));
                }

                if (!string.IsNullOrEmpty(filter.Year))
                {
                    query = query.Where(s => s.Year == filter.Year);
                }

                if (!string.IsNullOrEmpty(filter.Semester))
                {
                    query = query.Where(s => s.Enrollments.Any(e => e.AssignedSubject.Subject.Semester == filter.Semester));
                }

                if (filter.HasEnrollments.HasValue)
                {
                    if (filter.HasEnrollments.Value)
                        query = query.Where(s => s.Enrollments.Any());
                    else
                        query = query.Where(s => !s.Enrollments.Any());
                }

                // Apply sorting
                switch (filter.SortBy.ToLower())
                {
                    case "regdnumber":
                        query = filter.SortOrder.ToUpper() == "DESC" ?
                            query.OrderByDescending(s => s.RegdNumber) :
                            query.OrderBy(s => s.RegdNumber);
                        break;
                    case "email":
                        query = filter.SortOrder.ToUpper() == "DESC" ?
                            query.OrderByDescending(s => s.Email) :
                            query.OrderBy(s => s.Email);
                        break;
                    case "year":
                        query = filter.SortOrder.ToUpper() == "DESC" ?
                            query.OrderByDescending(s => s.Year) :
                            query.OrderBy(s => s.Year);
                        break;
                    default:
                        query = filter.SortOrder.ToUpper() == "DESC" ?
                            query.OrderByDescending(s => s.FullName) :
                            query.OrderBy(s => s.FullName);
                        break;
                }

                var students = await query.ToListAsync();

                var result = students.Select(s => new StudentDetailDto
                {
                    StudentId = s.Id,
                    FullName = s.FullName,
                    RegdNumber = s.RegdNumber,
                    Email = s.Email,
                    Year = s.Year,
                    Department = s.Department,
                    TotalEnrollments = s.Enrollments?.Count ?? 0,
                    IsActive = true,
                    EnrolledSubjects = s.Enrollments?.Select(e => new EnrolledSubjectInfo
                    {
                        EnrollmentId = e.StudentEnrollmentId,
                        SubjectId = e.AssignedSubject.SubjectId,
                        SubjectName = e.AssignedSubject.Subject.Name,
                        FacultyName = e.AssignedSubject.Faculty.Name,
                        Semester = e.AssignedSubject.Subject.Semester ?? "",
                        Year = e.AssignedSubject.Subject.Year,
                        IsActive = true
                    }).ToList() ?? new List<EnrolledSubjectInfo>()
                }).ToList();

                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error filtering students: {ex.Message}" });
            }
        }

        /// <summary>
        /// Admin logout
        /// </summary>
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
    }

    /// <summary>
    /// Request model for faculty-subject assignment
    /// </summary>
    public class FacultySubjectAssignmentRequest
    {
        public int SubjectId { get; set; }
        public List<int> FacultyIds { get; set; } = new List<int>();
    }

    /// <summary>
    /// Request model for removing faculty assignment
    /// </summary>
    public class RemoveFacultyAssignmentRequest
    {
        public int AssignedSubjectId { get; set; }
    }
}