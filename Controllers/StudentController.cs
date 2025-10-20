using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TutorLiveMentor.Models;
using TutorLiveMentor.Services;

namespace TutorLiveMentor.Controllers
{
    public class StudentController : Controller
    {
        private readonly AppDbContext _context;
        private readonly SignalRService _signalRService;

        public StudentController(AppDbContext context, SignalRService signalRService)
        {
            _context = context;
            _signalRService = signalRService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(StudentRegistrationModel model)
        {
            if (ModelState.IsValid)
            {
                // Convert registration number to uppercase on server side as well
                model.RegdNumber = model.RegdNumber?.ToUpper();
                
                // Check if student with this registration number already exists
                if (await _context.Students.AnyAsync(s => s.Id == model.RegdNumber))
                {
                    ModelState.AddModelError("RegdNumber", "Registration number is already registered.");
                    return View(model);
                }
                
                if (await _context.Students.AnyAsync(s => s.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "Email is already registered.");
                    return View(model);
                }

                var student = new Student
                {
                    Id = model.RegdNumber, // Set Id equal to RegdNumber
                    FullName = model.FullName,
                    RegdNumber = model.RegdNumber,
                    Year = model.Year,
                    Department = model.Department,
                    Email = model.Email,
                    Password = model.Password,
                    SelectedSubject = "" // Initialize property
                };

                _context.Students.Add(student);
                await _context.SaveChangesAsync();

                // Notify system of new user registration
                await _signalRService.NotifyUserActivity(student.FullName, "Student", "Registered", $"New student registered: {student.RegdNumber}");

                TempData["SuccessMessage"] = "Registration successful! Please log in now.";
                return RedirectToAction("Login");
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            // Simple, reliable login without complex debugging
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Find student with matching credentials
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.Email == model.Email && s.Password == model.Password);

                if (student == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid Email or Password.");
                    return View(model);
                }

                // Clear any existing session
                HttpContext.Session.Clear();

                // Set session values - now using string Id
                HttpContext.Session.SetString("StudentId", student.Id);
                HttpContext.Session.SetString("StudentName", student.FullName);

                // Force session to be saved immediately
                await HttpContext.Session.CommitAsync();

                // Simple redirect without SignalR to avoid complications
                return RedirectToAction("MainDashboard", "Student");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Login error: {ex.Message}");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult MainDashboard()
        {
            // Simple session check
            var studentId = HttpContext.Session.GetString("StudentId");
            
            if (string.IsNullOrEmpty(studentId))
            {
                TempData["ErrorMessage"] = "Please login to access the dashboard.";
                return RedirectToAction("Login");
            }
            
            // If we get here, student is logged in
            ViewBag.StudentId = studentId;
            ViewBag.StudentName = HttpContext.Session.GetString("StudentName");
            
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var studentId = HttpContext.Session.GetString("StudentId");
            if (string.IsNullOrEmpty(studentId))
            {
                return RedirectToAction("Login");
            }

            var student = await _context.Students
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.AssignedSubject)
                    .ThenInclude(asub => asub.Subject)
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.AssignedSubject)
                    .ThenInclude(asub => asub.Faculty)
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null)
            {
                return NotFound();
            }

            // Return the student model directly as the Dashboard view expects Student model
            return View(student);
        }

        [HttpPost]
        public async Task<IActionResult> SelectSubject(int assignedSubjectId)
        {
            var studentId = HttpContext.Session.GetString("StudentId");
            if (string.IsNullOrEmpty(studentId))
            {
                return RedirectToAction("Login");
            }

            var student = await _context.Students
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.AssignedSubject)
                    .ThenInclude(a => a.Subject)
                .FirstOrDefaultAsync(s => s.Id == studentId);
            
            var assignedSubject = await _context.AssignedSubjects
                .Include(a => a.Subject)
                .Include(a => a.Faculty)
                .FirstOrDefaultAsync(a => a.AssignedSubjectId == assignedSubjectId);

            if (student == null || assignedSubject == null)
            {
                return NotFound();
            }

            // Check if student has already enrolled in this specific assigned subject (same faculty)
            if (student.Enrollments.Any(e => e.AssignedSubjectId == assignedSubjectId))
            {
                TempData["ErrorMessage"] = "You have already enrolled with this faculty for this subject.";
                return RedirectToAction("SelectSubject");
            }

            // Check if student has already enrolled in this subject with any faculty
            if (student.Enrollments.Any(e => e.AssignedSubject.SubjectId == assignedSubject.SubjectId))
            {
                TempData["ErrorMessage"] = $"You have already enrolled in {assignedSubject.Subject.Name} with another faculty.";
                return RedirectToAction("SelectSubject");
            }

            // CHANGED: Limit from 60 to 20 students for testing
            if (assignedSubject.SelectedCount >= 20)
            {
                TempData["ErrorMessage"] = "This subject is already full (maximum 20 students).";
                return RedirectToAction("SelectSubject");
            }

            // Create a new enrollment
            var enrollment = new StudentEnrollment
            {
                StudentId = student.Id,
                AssignedSubjectId = assignedSubject.AssignedSubjectId
            };
            _context.StudentEnrollments.Add(enrollment);

            // Update selected subjects list (comma-separated)
            var enrolledSubjects = student.Enrollments?.Select(e => e.AssignedSubject.Subject.Name).ToList() ?? new List<string>();
            enrolledSubjects.Add(assignedSubject.Subject.Name);
            student.SelectedSubject = string.Join(", ", enrolledSubjects.Distinct());

            assignedSubject.SelectedCount++;

            await _context.SaveChangesAsync();

            // ?? REAL-TIME NOTIFICATION: Notify all connected users about the selection
            await _signalRService.NotifySubjectSelection(assignedSubject, student);

            // Check if subject is now full and notify availability change
            if (assignedSubject.SelectedCount >= 20)
            {
                await _signalRService.NotifySubjectAvailability(
                    assignedSubject.Subject.Name, 
                    assignedSubject.Year, 
                    assignedSubject.Department, 
                    false);
            }

            TempData["SuccessMessage"] = $"Successfully enrolled in {assignedSubject.Subject.Name} with {assignedSubject.Faculty.Name}.";
            return RedirectToAction("SelectSubject");
        }

        [HttpPost]
        public async Task<IActionResult> UnenrollSubject(int assignedSubjectId)
        {
            var studentId = HttpContext.Session.GetString("StudentId");
            if (string.IsNullOrEmpty(studentId))
            {
                return RedirectToAction("Login");
            }

            var student = await _context.Students
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.AssignedSubject)
                    .ThenInclude(a => a.Subject)
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.AssignedSubject)
                    .ThenInclude(a => a.Faculty)
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null)
            {
                return NotFound();
            }

            var enrollment = student.Enrollments.FirstOrDefault(e => e.AssignedSubjectId == assignedSubjectId);
            if (enrollment == null)
            {
                TempData["ErrorMessage"] = "You are not enrolled in this subject.";
                return RedirectToAction("SelectSubject");
            }

            var assignedSubject = await _context.AssignedSubjects
                .Include(a => a.Subject)
                .Include(a => a.Faculty)
                .FirstOrDefaultAsync(a => a.AssignedSubjectId == assignedSubjectId);

            if (assignedSubject != null)
            {
                var wasFullBefore = assignedSubject.SelectedCount >= 20;
                assignedSubject.SelectedCount = Math.Max(0, assignedSubject.SelectedCount - 1);

                // ?? REAL-TIME NOTIFICATION: Notify all connected users about the unenrollment
                await _signalRService.NotifySubjectUnenrollment(assignedSubject, student);

                // If subject was full and now has space, notify availability change
                if (wasFullBefore && assignedSubject.SelectedCount < 20)
                {
                    await _signalRService.NotifySubjectAvailability(
                        assignedSubject.Subject.Name, 
                        assignedSubject.Year, 
                        assignedSubject.Department, 
                        true);
                }
            }

            _context.StudentEnrollments.Remove(enrollment);

            // Update selected subjects list
            var remainingEnrollments = student.Enrollments.Where(e => e.AssignedSubjectId != assignedSubjectId).ToList();
            var enrolledSubjects = remainingEnrollments.Select(e => e.AssignedSubject.Subject.Name).ToList();
            student.SelectedSubject = enrolledSubjects.Any() ? string.Join(", ", enrolledSubjects.Distinct()) : "";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Successfully unenrolled from {assignedSubject?.Subject.Name}.";
            return RedirectToAction("SelectSubject");
        }

        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var studentId = HttpContext.Session.GetString("StudentId");
            if (string.IsNullOrEmpty(studentId))
            {
                return RedirectToAction("Login");
            }
            var student = await _context.Students.FindAsync(studentId);
            if (student == null)
            {
                return NotFound();
            }
            return View(student);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Student model)
        {
            var studentId = HttpContext.Session.GetString("StudentId");
            if (string.IsNullOrEmpty(studentId))
            {
                return RedirectToAction("Login");
            }

            // Ensure the model ID matches the logged-in student's ID
            if (model.Id != studentId)
            {
                return BadRequest();
            }

            if (ModelState.IsValid)
            {
                var studentToUpdate = await _context.Students.FindAsync(studentId);
                if (studentToUpdate == null)
                {
                    return NotFound();
                }

                studentToUpdate.FullName = model.FullName;
                studentToUpdate.Year = model.Year;
                studentToUpdate.Department = model.Department;

                await _context.SaveChangesAsync();

                // Notify system of profile update
                await _signalRService.NotifyUserActivity(studentToUpdate.FullName, "Student", "Profile Updated", $"Student updated their profile information");

                TempData["SuccessMessage"] = "Profile updated!";
                return RedirectToAction("Dashboard");
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            var studentId = HttpContext.Session.GetString("StudentId");
            if (!string.IsNullOrEmpty(studentId))
            {
                var student = await _context.Students.FindAsync(studentId);
                if (student != null)
                {
                    await _signalRService.NotifyUserActivity(student.FullName, "Student", "Logged Out", "Student logged out of the system");
                }
            }

            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        [HttpGet]
        public async Task<IActionResult> SelectSubject()
        {
            var studentId = HttpContext.Session.GetString("StudentId");
            if (string.IsNullOrEmpty(studentId))
            {
                return RedirectToAction("Login");
            }

            var student = await _context.Students
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.AssignedSubject)
                    .ThenInclude(asub => asub.Subject)
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.AssignedSubject)
                    .ThenInclude(asub => asub.Faculty)
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null)
            {
                return NotFound();
            }

            // Get all available subjects for the student's year
            var yearMap = new Dictionary<string, int> { { "I", 1 }, { "II", 2 }, { "III", 3 }, { "IV", 4 } };
            var studentYearKey = student.Year?.Replace(" Year", "")?.Trim() ?? "";
            
            var availableSubjects = new List<AssignedSubject>();
            if (yearMap.TryGetValue(studentYearKey, out int studentYear))
            {
                // CHANGED: Filter subjects that have less than 20 students (was 60)
                availableSubjects = await _context.AssignedSubjects
                   .Include(a => a.Subject)
                   .Include(a => a.Faculty)
                   .Where(a => a.Year == studentYear && a.SelectedCount < 20)
                   .ToListAsync();

                // Filter out subjects where student has already enrolled
                var enrolledSubjectIds = student.Enrollments?.Select(e => e.AssignedSubject.SubjectId).ToList() ?? new List<int>();
                availableSubjects = availableSubjects.Where(a => !enrolledSubjectIds.Contains(a.SubjectId)).ToList();
            }

            var viewModel = new StudentDashboardViewModel
            {
                Student = student,
                AvailableSubjectsGrouped = availableSubjects.GroupBy(s => s.Subject.Name)
            };

            return View(viewModel);
        }

        /*
        [HttpGet]
        public async Task<IActionResult> AssignedFaculty()
        {
            var studentId = HttpContext.Session.GetInt32("StudentId");
            if (studentId == null)
            {
                return RedirectToAction("Login");
            }

            var student = await _context.Students
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.AssignedSubject)
                    .ThenInclude(asub => asub.Subject)
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.AssignedSubject)
                    .ThenInclude(asub => asub.Faculty)
                .FirstOrDefaultAsync(s => s.Id == studentId.Value);

            if (student == null)
            {
                return NotFound();
            }

            return View(student);
        }
        */

        [HttpGet]
        public async Task<IActionResult> ChangePassword()
        {
            var studentId = HttpContext.Session.GetString("StudentId");
            if (string.IsNullOrEmpty(studentId))
            {
                return RedirectToAction("Login");
            }

            var student = await _context.Students.FindAsync(studentId);
            if (student == null)
            {
                return NotFound();
            }

            var model = new ChangePasswordViewModel
            {
                StudentId = student.Id,
                StudentName = student.FullName
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            var studentId = HttpContext.Session.GetString("StudentId");
            if (string.IsNullOrEmpty(studentId))
            {
                return RedirectToAction("Login");
            }

            if (model.StudentId != studentId)
            {
                return BadRequest();
            }

            if (ModelState.IsValid)
            {
                var student = await _context.Students.FindAsync(studentId);
                if (student == null)
                {
                    return NotFound();
                }

                // Verify current password
                if (student.Password != model.CurrentPassword)
                {
                    ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                    return View(model);
                }

                // Update password
                student.Password = model.NewPassword;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Password changed successfully!";
                return RedirectToAction("Dashboard");
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult TestEmoji()
        {
            // Simple test view to check emoji display
            return View();
        }

        [HttpGet]
        public IActionResult TestSession()
        {
            // Test session functionality
            HttpContext.Session.SetString("TestKey", "TestValue");
            var testValue = HttpContext.Session.GetString("TestKey");
            
            var debugInfo = new
            {
                SessionId = HttpContext.Session.Id,
                TestValue = testValue,
                StudentId = HttpContext.Session.GetString("StudentId"), // Changed to string
                SessionIsAvailable = HttpContext.Session.IsAvailable
            };
            
            return Json(debugInfo);
        }

        [HttpGet]
        public async Task<IActionResult> TestLogin()
        {
            // Test login with a known student (for debugging)
            var student = await _context.Students.FirstOrDefaultAsync();
            if (student != null)
            {
                HttpContext.Session.SetString("StudentId", student.Id); // Changed to string
                await HttpContext.Session.CommitAsync();
                
                return Json(new { 
                    Message = "Test login successful", 
                    StudentId = student.Id, 
                    StudentName = student.FullName,
                    SessionId = HttpContext.Session.Id
                });
            }
            
            return Json(new { Message = "No students found in database" });
        }

        [HttpGet]
        public async Task<IActionResult> DebugLogin()
        {
            try
            {
                // Test database connection
                var studentCount = await _context.Students.CountAsync();
                var sampleStudents = await _context.Students
                    .Take(3)
                    .Select(s => new { s.Id, s.FullName, s.Email })
                    .ToListAsync();

                // Test session
                HttpContext.Session.SetString("DebugTest", "Working");
                var sessionTest = HttpContext.Session.GetString("DebugTest");

                var debugInfo = new
                {
                    DatabaseConnection = "Connected",
                    TotalStudents = studentCount,
                    SampleStudents = sampleStudents.Cast<object>().ToList(),
                    SessionWorking = sessionTest == "Working",
                    SessionId = HttpContext.Session.Id,
                    CurrentStudentId = HttpContext.Session.GetString("StudentId"), // Changed to string
                    Timestamp = DateTime.Now
                };

                return Json(debugInfo);
            }
            catch (Exception ex)
            {
                var errorInfo = new
                {
                    DatabaseConnection = $"Error: {ex.Message}",
                    TotalStudents = 0,
                    SampleStudents = new List<object>(),
                    SessionWorking = false,
                    SessionId = HttpContext.Session.Id,
                    CurrentStudentId = HttpContext.Session.GetString("StudentId"), // Changed to string
                    Timestamp = DateTime.Now,
                    Error = ex.Message
                };

                return Json(errorInfo);
            }
        }

        [HttpGet]
        public async Task<IActionResult> DebugCredentials()
        {
            try
            {
                // Get all students with their exact credentials (for debugging only)
                var allStudents = await _context.Students
                    .Select(s => new { 
                        s.Id, 
                        s.FullName, 
                        s.Email, 
                        PasswordLength = s.Password.Length,
                        PasswordHash = s.Password.Substring(0, Math.Min(3, s.Password.Length)) + "***" // Show first 3 chars only for security
                    })
                    .ToListAsync();

                var debugInfo = new
                {
                    TotalStudents = allStudents.Count,
                    Students = allStudents,
                    Message = "Use the exact email from this list to test login"
                };

                return Json(debugInfo);
            }
            catch (Exception ex)
            {
                return Json(new { Error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DebugTestLogin(string email, string password)
        {
            try
            {
                Console.WriteLine($"?? DEBUG TEST LOGIN: Email='{email}', Password='{password}'");

                // Check if student exists with this exact email
                var studentWithEmail = await _context.Students
                    .Where(s => s.Email == email)
                    .Select(s => new { s.Id, s.FullName, s.Email, PasswordLength = s.Password.Length })
                    .FirstOrDefaultAsync();

                if (studentWithEmail == null)
                {
                    Console.WriteLine($"? No student found with email: '{email}'");
                    return Json(new { 
                        Success = false, 
                        Message = "No student found with that email",
                        Email = email
                    });
                }

                // Check password match
                var studentWithCredentials = await _context.Students
                    .FirstOrDefaultAsync(s => s.Email == email && s.Password == password);

                if (studentWithCredentials == null)
                {
                    Console.WriteLine($"? Password mismatch for email: '{email}'");
                    return Json(new { 
                        Success = false, 
                        Message = "Email found but password doesn't match",
                        Email = email,
                        StudentFound = studentWithEmail
                    });
                }

                // SUCCESS - Now try to log in
                Console.WriteLine($"? Credentials match! Logging in student: {studentWithCredentials.FullName}");

                // Set session - now using string
                HttpContext.Session.SetString("StudentId", studentWithCredentials.Id);
                await HttpContext.Session.CommitAsync();

                // Verify session was set
                var sessionCheck = HttpContext.Session.GetString("StudentId");
                Console.WriteLine($"? Session set. StudentId in session: {sessionCheck}");

                return Json(new { 
                    Success = true, 
                    Message = "Login successful!",
                    StudentId = studentWithCredentials.Id,
                    StudentName = studentWithCredentials.FullName,
                    SessionId = HttpContext.Session.Id,
                    RedirectUrl = Url.Action("MainDashboard", "Student")
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? DEBUG TEST LOGIN ERROR: {ex.Message}");
                return Json(new { 
                    Success = false, 
                    Message = ex.Message,
                    Error = ex.ToString()
                });
            }
        }

        [HttpGet]
        public IActionResult TestRouting()
        {
            return Json(new { 
                Message = "? Routing works!", 
                Controller = "Student", 
                Action = "TestRouting",
                Time = DateTime.Now,
                SessionId = HttpContext.Session.Id
            });
        }

        [HttpGet]  
        public async Task<IActionResult> SimpleDebug()
        {
            try
            {
                var studentCount = await _context.Students.CountAsync();
                var students = await _context.Students.Take(5).ToListAsync();
                
                return Json(new {
                    Success = true,
                    Message = "Database connected successfully",
                    StudentCount = studentCount,
                    Students = students.Select(s => new { s.Id, s.FullName, s.Email }).ToList(),
                    SessionWorking = HttpContext.Session.IsAvailable,
                    SessionId = HttpContext.Session.Id
                });
            }
            catch (Exception ex)
            {
                return Json(new {
                    Success = false,
                    Error = ex.Message,
                    Message = "Database connection failed"
                });
            }
        }
    }

    // View model for the dashboard
    public class StudentDashboardViewModel
    {
        public Student Student { get; set; }
        public IEnumerable<IGrouping<string, AssignedSubject>> AvailableSubjectsGrouped { get; set; }
    }}