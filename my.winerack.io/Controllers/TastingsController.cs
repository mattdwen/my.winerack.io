﻿using System.Linq;
using System.Net;
using System.Web.Mvc;
using winerack.Helpers.Authentication;
using winerack.Models;

namespace winerack.Controllers {

	[Authorize]
	public class TastingsController : Controller {

		#region Declarations

		private ApplicationDbContext db = new ApplicationDbContext();

		#endregion Declarations

		#region Actions

		#region Details

		// GET: Tastings/Details/5
		[TastingAuthentication]
		public ActionResult Details(int? id) {
			if (id == null) {
				return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
			}

			var tasting = db.Tastings.Find(id);

			if (tasting == null) {
				return HttpNotFound();
			}

			return View(tasting);
		}

		#endregion Details

		#region Create

		// GET: Tastings/Create
		[StoredBottleAuthenticationAttribute(IdParameter = "storedBottleId")]
		public ActionResult Create(int? storedBottleId) {
			if (storedBottleId == null) {
				return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
			}

			var tasting = new Tasting();

			tasting.StoredBottle = db.StoredBottles
				.Include("Purchase.Bottle.Wine")
				.Where(b => b.ID == storedBottleId)
				.FirstOrDefault();

			return View(tasting);
		}

		// POST: Tastings/Create
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Create([Bind(Include = "StoredBottleId,TastedOn,Notes")]Tasting tasting) {
			if (ModelState.IsValid) {
				db.Tastings.Add(tasting);
				db.SaveChanges();
				return RedirectToAction("Details", new { id = tasting.StoredBottleID });
			}

			tasting.StoredBottle = db.StoredBottles
					.Include("Purchase.Bottle.Wine")
					.Where(b => b.ID == tasting.StoredBottleID)
					.FirstOrDefault(); ;

			return View(tasting);
		}

		#endregion Create

		#endregion Actions
	}
}