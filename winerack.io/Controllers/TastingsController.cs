﻿using System.Linq;
using System.Web;
using System.Web.Mvc;
using winerack.Models;
using Microsoft.AspNet.Identity;
using winerack.Logic;
using System;
using winerack.ViewModels;

namespace winerack.Controllers {

    [Authorize]
    public class TastingsController : Controller {

        #region Declarations

        private ApplicationDbContext db = new ApplicationDbContext();

        #endregion Declarations

        #region Private Methods

        private TastingEditViewModel GetCreateViewModel(TastingEditViewModel model = null)
        {
            if (model == null) {
                model = new TastingEditViewModel {
                    TastingDate = DateTime.Now
                };
            }

            var user = db.Users.Find(User.Identity.GetUserId());
            model.HasFacebook = (user.Credentials.Where(c => c.CredentialType == CredentialTypes.Facebook).FirstOrDefault() != null);
            model.HasTumblr = (user.Credentials.Where(c => c.CredentialType == CredentialTypes.Tumblr).FirstOrDefault() != null);
            model.HasTwitter = (user.Credentials.Where(c => c.CredentialType == CredentialTypes.Twitter).FirstOrDefault() != null);

            return model;
        }

        private void PopulateViewBag()
        {
            ViewBag.Country = new SelectList(Country.GetCountries(), "ID", "Name");
            ViewBag.Varietals = db.Varietals.OrderBy(v => v.Name).Select(x => new SelectListItem {
                Text = x.Name,
                Value = x.ID.ToString()
            }).ToList();
            ViewBag.Styles = db.Styles.OrderBy(s => s.Name).ToList();
        }

        #endregion Private Methods

        #region Actions

        #region Create

        // GET: /tastings/create
        public ActionResult Create() {
            var model = GetCreateViewModel();

            PopulateViewBag();
            return View(model);
        }

        // POST: /tastings/create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(TastingEditViewModel model, HttpPostedFileBase photo) {
            if (ModelState.IsValid) {
                // Get the wine
                var wineLogic = new WineLogic(db);
                var wine = wineLogic.Create_Wine(model);

                // Create the tasting
                var tasting = new Tasting {
                    UserID = User.Identity.GetUserId(),
                    WineID = wine.ID,
                    TastedOn = model.TastingDate,
                    Notes = model.TastingNotes
                };

                // Save the photo
                if (photo != null && photo.ContentLength > 0) {
                    var blobHandler = new Logic.BlobHandler("tastings");
                    tasting.ImageID = blobHandler.UploadImage(photo, Images.GetSizes(ImageSizeSets.Standard));
                }

                db.Tastings.Add(tasting);
                db.SaveChanges();

                // Push to activity log
                var activityLogic = new Logic.Activities(db);
                var actvity = activityLogic.Publish(User.Identity.GetUserId(), ActivityVerbs.Tasted, tasting.ID, tasting.WineID);
                db.SaveChanges();

                wine = db.Wines.Find(wine.ID);

                // Share
                if (model.PostFacebook) {
                    var facebook = new Logic.Social.Facebook(db);
                    facebook.TasteWine(User.Identity.GetUserId(), tasting.ID);
                }

                if (model.PostTumblr && tasting.ImageID.HasValue) {
                    var tumblr = new Logic.Social.Tumblr(db);
                    var caption = wine.FullDescription;
                    var imageUrl = "https://winerack.blob.core.windows.net/tastings/" + tasting.ImageID.Value.ToString() + "_lg.jpg";
                    tumblr.PostPhoto(User.Identity.GetUserId(), imageUrl, caption);
                }

                if (model.PostTwitter) {
                    var twitter = new Logic.Social.Twitter(db);
                    var tweet = "I'm tasting a " + wine.FullDescription;
                    var url = "http://www.winerack.io/tastings/" + tasting.ID.ToString();
                    twitter.Tweet(User.Identity.GetUserId(), tweet, url);
                }

                return Redirect("/tastings/" + tasting.ID.ToString());
            }

            PopulateViewBag();

            model = GetCreateViewModel(model);

            return View(model);
        }

        #endregion Create

        #region Details

        // GET: /tastings/5
        [AllowAnonymous]
        [Route("tastings/{id:int}")]
        public ActionResult Details(int id) {
            var tasting = db.Tastings.Find(id);
            return View(tasting);
        }

        #endregion Details

        #endregion Actions
    }
}