﻿using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using usue_online_tests.Data;
using usue_online_tests.Models;
using usue_online_tests.Models.View;
using usue_online_tests.Report;

namespace usue_online_tests.Controllers
{
    [Authorize]
    public class Profile : Controller
    {
        private DataContext DataContext { get; }
        private GetUserByCookie GetUserByCookie { get; }
        private ReportMaker ReportMaker { get; }

        public Profile(DataContext dataContext, GetUserByCookie getUserByCookie, ReportMaker reportMaker)
        {
            DataContext = dataContext;
            GetUserByCookie = getUserByCookie;
            ReportMaker = reportMaker;
        }

        public IActionResult Index()
        {
            User user = GetUserByCookie.GetUser();

            if (user == null)
            {
                Response.Cookies.Delete(".AspNetCore.Cookies");
                return LocalRedirect("/login");
            }

            if (user.Role == Roles.Teacher)
            {
                // проверяем все тесты и завершаем те, у которых вышло время на прохождение
                FinishStartedExams();

                // лист готовых отчетов
                //var completedExamsPairs = DataContext.Exams
                //    .Include(exam => exam.Preset)
                //    .SelectMany(exam => DataContext.UserExamResults
                //            .Where(result => result.Exam.Id == exam.Id && result.Exam.Preset.Owner == user),
                //        (exam, examResult) => new { exam, examResult });

                var completedExamsPairs2 = DataContext.Exams
                    .Include(exam => exam.Preset)
                    .Where(exam => exam.Preset.Owner.Id == user.Id).ToArray();

                //var timeNow = DateTime.Now.ToNowEkb();

                //Exam[] completedExams = completedExamsPairs.Where(t =>
                //        //t.examResult.IsCompleted == true &&
                //        true)
                //    .Select(t => t.exam).ToArray();

                //ExamResult[] examResults = completedExams.Select(exam => new ExamResult
                //{
                //    Exam = exam,
                //    Results = DataContext.UserExamResults
                //        .Where(result => result.Exam == exam)
                //        .Include(result => result.User)
                //        .ToArray()
                //}).Distinct(new EqualityComparerExamResult()).ToArray();

                ExamResult[] examResults2 = completedExamsPairs2.Select(exam => new ExamResult
                {
                    Exam = exam,
                    Results = DataContext.UserExamResults
                        .Where(result => result.Exam == exam)
                        .Include(result => result.User)
                        .ToArray()
                }).Distinct(new EqualityComparerExamResult()).ToArray();

                TeacherProfileWrapper profileWrapper = new TeacherProfileWrapper()
                {
                    User = user,
                    ExamResults = examResults2
                };

                return View(profileWrapper);
            }

            UserProfileWrapper userProfileWrapper = new UserProfileWrapper
            {
                User = user,
                ExamResults = DataContext.UserExamResults
                    .Where(result => result.IsCompleted && result.User.Id == user.Id)
                    .Include(result => result.Exam.Preset)
                    .Include(result => result.ExamTestAnswers)
            };

            return View(userProfileWrapper);
        }

        private void FinishStartedExams()
        {
            var timeNow = DateTime.Now.ToNowEkb();
            Exam[] exams = DataContext.Exams.Where(exam => !exam.IsEnd && exam.DateTimeEnd < timeNow)
                .ToArray();

            foreach (var exam in exams)
            {
                exam.IsEnd = true;
            }

            DataContext.SaveChanges();
        }

        [Authorize(Roles = "Teacher, Admin")]
        public IActionResult CreateReport(int examId)
        {
            User user = GetUserByCookie.GetUser();

            if (!UserHasExam(user, examId))
                return StatusCode(400);

            return ReportMaker.CreateReport(examId);
        }

        private bool UserHasExam(User user, int examId)
        {
            return DataContext.Exams.Any(exam => exam.Id == examId && exam.Preset.Owner.Id == user.Id);
        }

        [HttpGet]
        public IActionResult ChangeUserTheme()
        {
            var user = GetUserByCookie.GetUser();
            user.IsDark = !user.IsDark;
            DataContext.SaveChanges();
            return StatusCode(200);
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }


        [HttpPost]
        public IActionResult ChangePassword(string password)
        {
            User user = GetUserByCookie.GetUser();

            if (string.IsNullOrEmpty(password) || password.Length < 5 || password.Length > 40)
            {
                return View("~/Views/Exam/ErrorPage.cshtml", "Пароль не соответствует требованиям");
            }

            user.Password = password;
            DataContext.SaveChanges();

            return LocalRedirect("/login/loginout");
        }
    }
}