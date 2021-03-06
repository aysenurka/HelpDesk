﻿using AutoMapper;
using HelpDesk.BLL.Account;
using HelpDesk.BLL.Helpers;
using HelpDesk.BLL.Repository.Abstracts;
using HelpDesk.BLL.Services.Senders;
using HelpDesk.Models.Entities;
using HelpDesk.Models.Enums;
using HelpDesk.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HelpDesk.Web.Controllers
{
    public class IssueController : BaseController
    {
        private readonly MembershipTools _membershipTools;
        private readonly IRepository<Issue, string> _issueRepo;
        private readonly IRepository<IssueLog, string> _issuelogRepo;
        private readonly IRepository<Photograph, string> _photographRepo;
        private readonly IRepository<Survey, string> _surveyRepo;
        private readonly IHostingEnvironment _hostingEnvironment;
        public IssueController(MembershipTools membershipTools, IRepository<Issue, string> issueRepo, IRepository<IssueLog, string> issuelogRepo, IRepository<Photograph, string> photographRepo, IHostingEnvironment hostingEnvironment, IRepository<Survey, string> surveyRepo) :base(membershipTools)
        {
            _membershipTools = membershipTools;
            _issueRepo = issueRepo;
            _issuelogRepo = issuelogRepo;
            _photographRepo = photographRepo;
            _surveyRepo = surveyRepo;
            _hostingEnvironment = hostingEnvironment;
        }

        [HttpGet]
        [Route("arizakayit_anasayfa")]
        public async Task<ActionResult> Index()
        {
            try
            {
                var user = await _membershipTools.UserManager.GetUserAsync(HttpContext.User);
                var id = user.Id;
                var data = _issueRepo.GetAll(x => x.CustomerId == id).Select(x => Mapper.Map<IssueVM>(x)).ToList();
                if (data != null)
                {
                    return View(data);
                }
            }
            catch (Exception ex)
            {
                var errorVM = new ErrorVM()
                {
                    Text = $"Bir hata oluştu {ex.Message}",
                    ActionName = "Index",
                    ControllerName = "Issue",
                    ErrorCode = "500"
                };
                TempData["ErrorMessage"] = JsonConvert.SerializeObject(errorVM);
                return RedirectToAction("Error500", "Home");
            }
            return View();
        }

        [HttpGet]
        [Route("yenikayitolustur")]
        [Authorize(Roles = "Admin, Customer")]
        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [Route("yenikayitolustur")]
        [Authorize(Roles = "Admin, Customer")]
        public async Task<ActionResult> Create(IssueVM model)
        {
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError("", "Hata Oluştu.");
                return RedirectToAction("Create", "Issue", model);
            }
            try
            {
                var user = await _membershipTools.UserManager.GetUserAsync(HttpContext.User);
                var issue = new Issue()
                {
                    Description = model.Description,
                    IssueState = model.IssueState,
                    Latitude=model.Latitude,
                    Longitude=model.Longitude,
                    ProductType = model.ProductType,
                    CustomerId = model.CustomerId,
                    PurchasedDate = model.PurchasedDate,
                    PhotoPath = model.PhotoPath,
                    ServiceCharge = model.ServiceCharge,
                    ClosedDate = model.ClosedDate,
                    CreatedDate = model.CreatedDate,
                    OperatorId = model.OperatorId,
                    TechReport = model.TechReport
                };
                switch (issue.ProductType)
                {
                    case ProductTypes.Buzdolabı:
                        if (issue.PurchasedDate.AddYears(1) > DateTime.Now)
                        {
                            issue.WarrantyState = true;
                        }
                        break;
                    case ProductTypes.BulaşıkMakinesi:
                        if (issue.PurchasedDate.AddYears(2) > DateTime.Now)
                        {
                            issue.WarrantyState = true;
                        }
                        break;
                    case ProductTypes.Fırın:
                        if (issue.PurchasedDate.AddYears(3) > DateTime.Now)
                        {
                            issue.WarrantyState = true;
                        }
                        break;
                    case ProductTypes.ÇamaşırMakinesi:
                        if (issue.PurchasedDate.AddYears(4) > DateTime.Now)
                        {
                            issue.WarrantyState = true;
                        }
                        break;
                    case ProductTypes.Mikrodalga:
                        if (issue.PurchasedDate.AddYears(5) > DateTime.Now)
                        {
                            issue.WarrantyState = true;
                        }
                        break;
                    default:
                        if (issue.PurchasedDate.AddYears(2) > DateTime.Now)
                        {
                            issue.WarrantyState = true;
                        }
                        break;
                }
                if (issue.WarrantyState)
                {
                    issue.ServiceCharge = 0;
                }

                var repo = _issueRepo;
                repo.Insert(issue);
                var fotorepo =_photographRepo;
                if (model.PostedPhoto.Count > 0)
                {
                    model.PostedPhoto.ForEach(async file =>
                    {
                        if (file == null || file.Length <= 0)
                        {
                            var filepath2 = Path.Combine("/assets/images/image-not-available.png");

                            using (var fileStream = new FileStream(filepath2, FileMode.Create))
                            {
                                await file.CopyToAsync(fileStream);
                            }
                            fotorepo.Insert(new Photograph()
                            {
                                IssueId = issue.Id,
                                Path = "/assets/images/image-not-available.png"
                            });

                            return;
                        }

                        var fileName = Path.GetFileNameWithoutExtension(file.FileName);
                        var extName = Path.GetExtension(file.FileName);
                        fileName = StringHelpers.UrlFormatConverter(fileName);
                        fileName += StringHelpers.GetCode();
                        var webpath = _hostingEnvironment.WebRootPath;
                        var directorypath = Path.Combine(webpath, "Uploads");
                        var filePath = Path.Combine(directorypath, fileName + extName);

                        if (!Directory.Exists(directorypath))
                        {
                            Directory.CreateDirectory(directorypath);
                        }
                        
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            file.CopyTo(fileStream);
                        }
                        fotorepo.Insert(new Photograph()
                        {
                            IssueId = issue.Id,
                            Path = "/Uploads/" + fileName + extName
                        });

                    });
                }

                var fotograflar = fotorepo.GetAll(x => x.IssueId == issue.Id).ToList();
                var foto = fotograflar.Select(x => x.Path).ToList();
                issue.PhotoPath = foto;
                repo.Update(issue);

                TempData["Message"] = "Arıza kaydınız başarı ile oluşturuldu.";

                var emailService = new EmailService();

                var body = $"Merhaba <b>{user.Name} {user.Surname}</b><br>Arıza kaydınız başarıyla oluşturuldu.Birimlerimiz sorunu çözmek için en kısa zamanda olay yerine intikal edecektir.<br><br> Ayrıntılı bilgi için telefon numaramız:<i>0212 684 75 33</i>";

                await emailService.SendAsync(new EmailModel()
                {
                    Body = body,
                    Subject = "Arıza kaydı oluşturuldu."
                }, user.Email);

                var issueLog = new IssueLog()
                {
                    IssueId = issue.Id,
                    Description = "Arıza Kaydı Oluşturuldu.",
                    FromWhom = "Müşteri"
                };
                _issuelogRepo.Insert(issueLog);

                return RedirectToAction("Index", "Issue");
            }
            catch (Exception ex)
            {
                var errorVM = new ErrorVM()
                {
                    Text = $"Bir hata oluştu {ex.Message}",
                    ActionName = "Create",
                    ControllerName = "Issue",
                    ErrorCode = "500"
                };
                TempData["ErrorMessage"] = JsonConvert.SerializeObject(errorVM);
                return RedirectToAction("Error500", "Home");
            }
        }

        [HttpGet]
        [Route("kayit_detay/{id}")]
        public ActionResult Details(string id)
        {
            var issue = _issueRepo.GetById(id);
            if (issue == null)
            {
                TempData["Message2"] = "Arıza kaydı bulunamadi.";
                return RedirectToAction("Index", "Issue");
            }
            var data = Mapper.Map<Issue, IssueVM>(issue);
            data.PhotoPath = _photographRepo.GetAll(x => x.IssueId == id).Select(y => y.Path).ToList();
            return View(data);
        }

        [HttpGet]
        [Authorize(Roles = "Customer")]
        public ActionResult Survey(string code)
        {
            try
            {
                var surveyRepo = _surveyRepo;
                var survey = surveyRepo.GetById(code);
                if (survey.IsDone == true)
                {
                    TempData["Message2"] = "Bu anket zaten tamamlanmış.";
                    return RedirectToAction("Index", "Home");
                }
                if (survey == null)
                {
                    return RedirectToAction("Index", "Home");
                }

                var data = Mapper.Map<Survey, SurveyVM>(survey);
                return View(data);
            }
            catch (Exception ex)
            {
                var errorVM = new ErrorVM()
                {
                    Text = $"Bir hata oluştu {ex.Message}",
                    ActionName = "Survey",
                    ControllerName = "Issue",
                    ErrorCode = "500"
                };
                TempData["ErrorMessage"] = JsonConvert.SerializeObject(errorVM);
                return RedirectToAction("Error500", "Home");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Customer")]
        public ActionResult Survey(SurveyVM model)
        {
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError("", "Hata Oluştu.");
                return RedirectToAction("Survey", "Issue", model);
            }
            try
            {
                var surveyRepo = _surveyRepo;
                var survey = surveyRepo.GetById(model.SurveyId);
                if (survey == null)
                {
                    return RedirectToAction("Index", "Home");
                }
                Mapper.Map<SurveyVM, Survey>(model, survey);
                //survey.Pricing = model.Pricing;
                //survey.Satisfaction = model.Satisfaction;
                //survey.Solving = model.Solving;
                //survey.Speed = model.Speed;
                //survey.TechPoint = model.TechPoint;
                //survey.Suggestions = model.Suggestions;
                survey.IsDone = true;
                surveyRepo.Update(survey);
                TempData["Message"] = "Anket tamamlandı.";
                return RedirectToAction("UserProfile", "Account");
            }
            catch (Exception ex)
            {
                var errorVM = new ErrorVM()
                {
                    Text = $"Bir hata oluştu {ex.Message}",
                    ActionName = "Survey",
                    ControllerName = "Issue",
                    ErrorCode = "500"
                };
                TempData["ErrorMessage"] = JsonConvert.SerializeObject(errorVM);
                return RedirectToAction("Error500", "Home");
            }
        }


        [HttpGet]
        [Route("IssueTimeline/{id}")]
        public ActionResult Timeline(string id)
        {
            var data = _issuelogRepo.GetAll(x => x.IssueId == id).OrderBy(x => x.CreatedDate).ToList();
            if (data == null)
                return RedirectToAction("Details", "Issue", id);
            return View(data);
        }

        [HttpGet]
        [Authorize(Roles = "Admin, Operator")]
        public ActionResult ListAll()
        {
            var data = _issueRepo.GetAll(x => x.ClosedDate != null);
            if (data == null)
                return RedirectToAction("Index", "Home");
            return View(data);
        }

    }
}