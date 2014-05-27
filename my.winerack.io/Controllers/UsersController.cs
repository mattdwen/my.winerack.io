﻿using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using winerack.Models;

namespace winerack.Controllers {

	[Authorize(Roles = "Administrators")]
	public class UsersController : Controller {

		#region Constructor

		public UsersController() {
			RoleManager = new ApplicationRoleManager(new RoleStore<IdentityRole>(db));
		}

		public UsersController(ApplicationUserManager userManager) {
			UserManager = userManager;
		}

		#endregion Constructor

		#region Constants

		#endregion Constants

		#region Declarations

		private ApplicationRoleManager _roleManager;
		private ApplicationUserManager _userManager;
		private ApplicationDbContext db = new ApplicationDbContext();

		#endregion Declarations

		#region Properties

		public ApplicationRoleManager RoleManager {
			get {
				return _roleManager;
			}
			set {
				_roleManager = value;
			}
		}

		public ApplicationUserManager UserManager {
			get {
				return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
			}
			private set {
				_userManager = value;
			}
		}

		#endregion Properties

		#region Private Methods

		private void AddErrors(IdentityResult result) {
			foreach (var error in result.Errors) {
				ModelState.AddModelError("", error);
			}
		}

		private async Task SendInvite(string userId) {
			string code = await UserManager.GenerateEmailConfirmationTokenAsync(userId);
			var callbackUrl = Url.Action("AcceptInvite", "Account", new { userId = userId, code = code }, protocol: Request.Url.Scheme);
			await UserManager.SendEmailAsync(
				userId,
				"winerack.io invitation",
				"Please create your account by clicking <a href=\"" + callbackUrl + "\">here</a>. When prompted to reset your password, enter your email address and desired password.");
		}

		#endregion Private Methods

		#region Actions

		#region Index

		// GET: Users
		public ActionResult Index() {
			return View(db.Users.ToList());
		}

		#endregion Index

		#region Details

		// GET: Users/Details/5
		public ActionResult Details(string id) {
			if (id == null) {
				return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
			}

			User user = db.Users.Find(id);

			if (user == null) {
				return HttpNotFound();
			}

			var model = new UserDetailsViewModel {
				Id = id,
				Administrator = UserManager.IsInRole(id, Helpers.ADMINISTRATOR_GROUP),
				Email = user.Email,
				Name = user.FirstName + " " + user.LastName,
				Verified = user.EmailConfirmed
			};

			return View(model);
		}

		#endregion Details

		#region Administrator

		public async Task<ActionResult> Administrator(string id) {
			if (id == null) {
				return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
			}

			// Check Administrators role exists
			if (!RoleManager.RoleExists(Helpers.ADMINISTRATOR_GROUP)) {
				RoleManager.Create(new IdentityRole(Helpers.ADMINISTRATOR_GROUP));
			}

			var user = await UserManager.FindByIdAsync(id);

			if (user == null) {
				return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
			}

			var inRole = await UserManager.IsInRoleAsync(id, Helpers.ADMINISTRATOR_GROUP);
			if (inRole) {
				await UserManager.RemoveFromRoleAsync(id, Helpers.ADMINISTRATOR_GROUP);
			} else {
				await UserManager.AddToRoleAsync(id, Helpers.ADMINISTRATOR_GROUP);
			}

			return RedirectToAction("Details", new { id = id });
		}

		#endregion Administrator

		#region Invite

		// GET: Users/invite
		public ActionResult Invite() {
			return View();
		}

		// POST: Users/invite
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<ActionResult> Invite(InviteUserViewModel model) {
			if (ModelState.IsValid) {
				var user = new User {
					Email = model.Email,
					UserName = model.Email,
					FirstName = model.FirstName,
					LastName = model.LastName
				};

				IdentityResult result = await UserManager.CreateAsync(user);

				if (result.Succeeded) {
					await SendInvite(user.Id);
					return RedirectToAction("Index");
				}

				AddErrors(result);
			}

			return View(model);
		}

		// Get: Users/ReInvite/5
		public async Task<ActionResult> ReInvite(string id) {
			if (id == null) {
				return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
			}

			var user = db.Users.Find(id);
			if (user == null) {
				return HttpNotFound();
			}

			await SendInvite(user.Id);
			return RedirectToAction("Details", new { id = user.Id });
		}

		#endregion Invite

		#endregion Actions

		#region Public Methods

		protected override void Dispose(bool disposing) {
			if (disposing) {
				db.Dispose();
			}
			base.Dispose(disposing);
		}

		#endregion Public Methods
	}
}