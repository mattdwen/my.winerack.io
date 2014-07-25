﻿using System.Linq;
using System.Web;
using System.Web.Mvc;
using winerack.Models;
using winerack.Models.TastingViewModels;
using Microsoft.AspNet.Identity;
using winerack.Logic;
using System;

namespace winerack.Controllers {

	[Authorize]
	public class TastingsController : Controller {

		#region Declarations

		private ApplicationDbContext db = new ApplicationDbContext();

		#endregion Declarations

		#region Private Methods

		private Region Create_Region(Create model) {
			var region = db.Regions
				.Where(r => r.Country == model.Country && r.Name == model.Region)
				.FirstOrDefault();

			if (region == null) {
				region = new Region {
					Country = model.Country,
					Name = model.Region
				};

				db.Regions.Add(region);
				db.SaveChanges();
			}

			return region;
		}

		private Vineyard Create_Vineyard(Create model) {
			var vineyard = db.Vineyards
				.Where(v => v.Name == model.Vineyard)
				.FirstOrDefault();

			if (vineyard == null) {
				vineyard = new Vineyard {
					Name = model.Vineyard
				};

				db.Vineyards.Add(vineyard);
				db.SaveChanges();
			}

			return vineyard;
		}

		private Wine Create_Wine(Create model) {
			var region = Create_Region(model);
			var vineyard = Create_Vineyard(model);

			var wine = db.Wines
				.Where(w => w.Name == model.WineName
					&& w.RegionID == region.ID
					&& w.VarietalID == model.VarietalID
					&& w.VineyardID == vineyard.ID
					&& w.Vintage == model.Vintage)
				.FirstOrDefault();

			if (wine == null) {
				wine = new Wine {
					Name = model.WineName,
					RegionID = region.ID,
					VarietalID = model.VarietalID,
					VineyardID = vineyard.ID,
					Vintage = model.Vintage
				};

				db.Wines.Add(wine);
				db.SaveChanges();
			}

			return wine;
		}

		#endregion Private Methods

		#region Actions

		#region Create

		// GET: /tastings/create
		public ActionResult Create() {
			var model = new Create {
				TastingDate = DateTime.Now
			};

			ViewBag.Country = new SelectList(Country.GetCountries(), "ID", "Name");
			ViewBag.VarietalID = db.Varietals.OrderBy(v => v.Name).Select(x => new SelectListItem {
				Text = x.Name,
				Value = x.ID.ToString()
			}).ToList();

			return View(model);
		}

		// POST: /tastings/create
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Create(Create model, HttpPostedFileBase photo) {
			if (ModelState.IsValid) {
				// Get the wine
				var wine = Create_Wine(model);

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
				Logic.ActivityStream.Publish(db, User.Identity.GetUserId(), ActivityVerbs.Tasted, tasting.ID);
				db.SaveChanges();

				return Redirect("/tastings/" + tasting.ID.ToString());
			}

			ViewBag.Country = new SelectList(Country.GetCountries(), "ID", "Name");
			ViewBag.VarietalID = db.Varietals.OrderBy(v => v.Name).Select(x => new SelectListItem {
				Text = x.Name,
				Value = x.ID.ToString()
			}).ToList();

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