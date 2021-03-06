﻿using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using winerack.Helpers.Authentication;
using winerack.Logic;
using winerack.Models;
using winerack.ViewModels;

namespace winerack.Controllers
{
    [Authorize]
    public class BottlesController : Controller
    {
        #region Declarations

        private ApplicationDbContext db = new ApplicationDbContext();

        #endregion Declarations

        #region Private Methods

        #region Create

        private Bottle Create_Bottle(BottleEditViewModel model)
        {
            var wineLogic = new WineLogic(db);
            var userId = User.Identity.GetUserId();
            var wine = wineLogic.Create_Wine(model);

            var bottle = db.Bottles
                .Where(b => b.OwnerID == userId && b.WineID == wine.ID)
                .FirstOrDefault();

            if (bottle == null) {
                bottle = new Bottle {
                    CreatedOn = DateTime.Now,
                    OwnerID = userId,
                    WineID = wine.ID,
                    CellarMin = model.CellarMin,
                    CellarMax = model.CellarMax
                };
                db.Bottles.Add(bottle);
                db.SaveChanges();
            }

            return bottle;
        }

        #endregion Create

        #region ViewBag

        private void PopulateViewBag()
        {
            ViewBag.Country = new SelectList(Country.GetCountries(), "ID", "Name");
            ViewBag.Varietals = db.Varietals.OrderBy(v => v.Name).Select(x => new SelectListItem {
                Text = x.Name,
                Value = x.ID.ToString()
            }).ToList();
            ViewBag.Styles = db.Styles.OrderBy(s => s.Name).ToList();
        }

        #endregion ViewBag

        #endregion Private Methods

        #region Protected Methods

        protected override void Dispose(bool disposing)
        {
            if (disposing) {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion Protected Methods

        #region Actions

        #region Index

        // GET: Bottles
        public ActionResult Index()
        {
            return View();
        }

        #endregion Index

        #region Details

        // GET: Bottles/Details/5
        [BottleAuthenticationAttribute]
        public ActionResult Details(int? id)
        {
            if (id == null) {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Bottle bottle = db.Bottles.Find(id);
            if (bottle == null) {
                return HttpNotFound();
            }
            return View(bottle);
        }

        #endregion Details

        #region Create

        // GET: Bottles/Create
        public ActionResult Create()
        {
            var user = db.Users.Find(User.Identity.GetUserId());

            var model = new BottleEditViewModel {
                PurchaseDate = DateTime.Now,
                PurchaseQuantity = 1,
                HasFacebook = (user.Credentials.Where(c => c.CredentialType == CredentialTypes.Facebook).FirstOrDefault() != null),
                HasTumblr = (user.Credentials.Where(c => c.CredentialType == CredentialTypes.Tumblr).FirstOrDefault() != null),
                HasTwitter = (user.Credentials.Where(c => c.CredentialType == CredentialTypes.Twitter).FirstOrDefault() != null)
            };

            PopulateViewBag();

            return View(model);
        }

        // POST: Bottles/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(BottleEditViewModel model, HttpPostedFileBase photo)
        {
            if (ModelState.IsValid) {
                // Get the bottle
                var bottle = Create_Bottle(model);

                // Create the purchase
                var purchase = new Purchase {
                    BottleID = bottle.ID,
                    IsGift = model.IsGift,
                    PurchasedOn = model.PurchaseDate,
                    PurchasePrice = model.PurchaseValue,
                    Quantity = model.PurchaseQuantity,
                    Notes = model.PurchaseNotes
                };

                // Save the photo
                if (photo != null && photo.ContentLength > 0) {
                    var blobHandler = new Logic.BlobHandler("purchases");
                    purchase.ImageID = blobHandler.UploadImage(photo, Images.GetSizes(ImageSizeSets.Standard));
                }

                // Add a stored bottle per quantity
                for (int i = 0; i < purchase.Quantity; i++) {
                    purchase.StoredBottles.Add(new StoredBottle());
                }

                db.Purchases.Add(purchase);
                db.SaveChanges();

                // Push to activity log
                var activityLogic = new Logic.Activities(db);
                activityLogic.Publish(User.Identity.GetUserId(), ActivityVerbs.Purchased, purchase.ID, bottle.WineID);
                activityLogic.SaveChanges();

                if (model.PostFacebook) {
                    var facebook = new Logic.Social.Facebook(db);
                    facebook.PurchaseWine(User.Identity.GetUserId(), purchase.ID);
                }

                if (model.PostTwitter) {
                    purchase = db.Purchases
                        .Where(p => p.ID == purchase.ID)
                        .Include("Bottle.Wine.Varietal")
                        .FirstOrDefault();
                    var twitter = new Logic.Social.Twitter(db);
                    var quantity = Helpers.ExtensionMethods.BottleQuantity(purchase.Quantity);
                    var verb = (purchase.IsGift) ? "been gifted" : "purchased";
                    var tweet = "I've " + verb + " " + quantity + " of " + purchase.Bottle.Wine.Description;
                    var url = "http://www.winerack.io/purchases/" + purchase.ID.ToString();
                    twitter.Tweet(User.Identity.GetUserId(), tweet, url);
                }

                return RedirectToAction("Details", new { id = bottle.ID });
            }

            var user = db.Users.Find(User.Identity.GetUserId());
            model.HasFacebook = (user.Credentials.Where(c => c.CredentialType == CredentialTypes.Facebook).FirstOrDefault() != null);
            model.HasTumblr = (user.Credentials.Where(c => c.CredentialType == CredentialTypes.Tumblr).FirstOrDefault() != null);
            model.HasTwitter = (user.Credentials.Where(c => c.CredentialType == CredentialTypes.Twitter).FirstOrDefault() != null);

            PopulateViewBag();

            return View(model);
        }

        #endregion Create

        #region Edit

        // GET: Bottles/Edit/5
        [BottleAuthenticationAttribute]
        public ActionResult Edit(int? id)
        {
            if (id == null) {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Bottle bottle = db.Bottles.Find(id);
            if (bottle == null) {
                return HttpNotFound();
            }
            ViewBag.OwnerID = new SelectList(db.Users, "Id", "Email", bottle.OwnerID);
            ViewBag.WineID = new SelectList(db.Wines, "ID", "Name", bottle.WineID);
            return View(bottle);
        }

        // POST: Bottles/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [BottleAuthenticationAttribute]
        public ActionResult Edit([Bind(Include = "ID,WineID,OwnerID,CreatedOn")] Bottle bottle)
        {
            if (ModelState.IsValid) {
                db.Entry(bottle).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.OwnerID = new SelectList(db.Users, "Id", "Email", bottle.OwnerID);
            ViewBag.WineID = new SelectList(db.Wines, "ID", "Name", bottle.WineID);
            return View(bottle);
        }

        #endregion Edit

        #region Delete

        // GET: Bottles/Delete/5
        [BottleAuthenticationAttribute]
        public ActionResult Delete(int? id)
        {
            if (id == null) {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Bottle bottle = db.Bottles.Find(id);
            if (bottle == null) {
                return HttpNotFound();
            }
            return View(bottle);
        }

        // POST: Bottles/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [BottleAuthenticationAttribute]
        public ActionResult DeleteConfirmed(int id)
        {
            Bottle bottle = db.Bottles.Find(id);
            db.Bottles.Remove(bottle);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        #endregion Delete

        #region Drink

        // GET: Bottles/Drink/5
        [BottleAuthentication]
        public ActionResult Drink(int? id)
        {
            if (id == null) {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var bottle = db.Bottles.Find(id);

            if (bottle == null) {
                return HttpNotFound();
            }

            return View(bottle);
        }

        #endregion Drink

        #region Window

        [HttpPost]
        [BottleAuthenticationAttribute]
        [ValidateAntiForgeryToken]
        public ActionResult Window([Bind(Include = "ID,CellarMin,CellarMax")] Bottle model)
        {
            if (model.ID < 1) {
                return HttpNotFound();
            }

            var bottle = db.Bottles.Find(model.ID);
            if (bottle == null) {
                return HttpNotFound();
            }

            bottle.CellarMin = model.CellarMin;
            bottle.CellarMax = model.CellarMax;
            db.Entry(bottle).State = EntityState.Modified;
            db.SaveChanges();

            return RedirectToAction("Details", new { id = model.ID });
        }

        #endregion

        #endregion Actions

        #region Partial Views

        public PartialViewResult Rack()
        {
            var userId = User.Identity.GetUserId();

            var bottles = db.Bottles
                .Where(b => b.OwnerID == userId)
                .ToList();

            var result = new List<RackBottleViewModel>();
            foreach (var bottle in bottles) {
                var openingsWithRating = db.Openings
                    .Where(o => o.StoredBottle.Purchase.BottleID == bottle.ID);

                double? rating = (openingsWithRating.Count() > 0)
                    ? openingsWithRating.Select(o => o.Rating).Average()
                    : (double?)null;

                result.Add(new RackBottleViewModel {
                    Bottle = bottle,
                    Rating = rating
                });
            }

            return PartialView(result);
        }

        #endregion Partial Views
    }
}