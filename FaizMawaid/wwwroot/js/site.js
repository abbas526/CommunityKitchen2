/* ==========================================================================
   Faiz al-Mawaid al-Burhaniyah Community Kitchen -- shared client helpers
   Loaded on every page (after jQuery + Bootstrap). Provides:
     - CK.session     localStorage-backed "who am I" + token store for
                       Family Head / Admin (JWT access token, refresh token,
                       and profile fields returned by /api/auth/login).
                       This IS the security boundary now -- every API call
                       other than the auth endpoints and the few explicitly
                       [AllowAnonymous] ones requires a valid token, enforced
                       server-side.
     - CK.api        jQuery-AJAX wrapper: attaches the bearer access token
                      for the current area (family/admin), transparently
                      refreshes an expired access token on a 401 and retries
                      the request once, and gives consistent error messages.
     - CK.auth       thin wrappers around the /api/auth/* endpoints
                      (login, refresh, logout, change-password, etc).
     - CK.nav         renders the shared top navbar on every page
     - CK.toast       small Bootstrap toast notifications
     - CK.fmt / CK.esc / CK.today  small formatting + escaping helpers
   ========================================================================== */
(function (window, $) {
  "use strict";

  var CK = {};

  // ---------------------------------------------------------------------
  // Session (JWT access token + refresh token, per area)
  // ---------------------------------------------------------------------
  var FAMILY_KEY = "ck_family_session";
  var ADMIN_KEY = "ck_admin_session";

  function readSession(key) {
    try {
      var raw = window.localStorage.getItem(key);
      return raw ? JSON.parse(raw) : null;
    } catch (e) {
      return null;
    }
  }
  function writeSession(key, value) {
    try { window.localStorage.setItem(key, JSON.stringify(value)); } catch (e) { /* ignore */ }
  }
  function clearSession(key) {
    try { window.localStorage.removeItem(key); } catch (e) { /* ignore */ }
  }

  /** Which stored session (if any) belongs to the page we're currently on. */
  function activeSessionKey() {
    var path = window.location.pathname;
    if (path.indexOf("/admin/") >= 0) { return ADMIN_KEY; }
    if (path.indexOf("/family/") >= 0) { return FAMILY_KEY; }
    return null;
  }

  // One in-flight refresh call per area, so several 401s in a row share a single request.
  var refreshInFlight = {};

  function refreshSessionAt(key) {
    if (refreshInFlight[key]) { return refreshInFlight[key]; }
    var current = readSession(key);
    if (!current || !current.refreshToken) {
      return $.Deferred().reject().promise();
    }
    var promise = CK.api.call("POST", "/api/auth/refresh", { refreshToken: current.refreshToken }, { skipAuth: true })
      .done(function (resp) {
        writeSession(key, resp);
      })
      .always(function () {
        delete refreshInFlight[key];
      });
    refreshInFlight[key] = promise;
    return promise;
  }

  CK.session = {
    setFamily: function (session) { writeSession(FAMILY_KEY, session); },
    getFamily: function () { return readSession(FAMILY_KEY); },
    clearFamily: function () { clearSession(FAMILY_KEY); },

    setAdmin: function (session) { writeSession(ADMIN_KEY, session); },
    getAdmin: function () { return readSession(ADMIN_KEY); },
    clearAdmin: function () { clearSession(ADMIN_KEY); },

    /** Call at the top of a family-only page. Redirects and returns null if not signed in. */
    requireFamily: function () {
      var s = CK.session.getFamily();
      if (!s || !s.familyId || !s.accessToken) {
        CK.session.clearFamily();
        window.location.replace(CK.rootPath() + "family/login.html");
        return null;
      }
      return s;
    },
    /** Call at the top of an admin-only page. Redirects and returns null if not signed in. */
    requireAdmin: function () {
      var s = CK.session.getAdmin();
      if (!s || !s.userId || !s.accessToken) {
        CK.session.clearAdmin();
        window.location.replace(CK.rootPath() + "admin/login.html");
        return null;
      }
      return s;
    },

    /** The bearer token for whichever area (family/admin) the current page belongs to. */
    getActiveAccessToken: function () {
      var key = activeSessionKey();
      if (key) {
        var s = readSession(key);
        return s && s.accessToken ? s.accessToken : null;
      }
      var fam = readSession(FAMILY_KEY);
      if (fam && fam.accessToken) { return fam.accessToken; }
      var adm = readSession(ADMIN_KEY);
      if (adm && adm.accessToken) { return adm.accessToken; }
      return null;
    },

    /** Refreshes the token for whichever area the current page belongs to, and stores the result. */
    refreshActive: function () {
      return refreshSessionAt(activeSessionKey() || FAMILY_KEY);
    },

    /** Clears whichever session belongs to this area and sends the user back to its login page. */
    handleAuthFailure: function () {
      var key = activeSessionKey();
      if (key === ADMIN_KEY) {
        CK.session.clearAdmin();
        window.location.replace(CK.rootPath() + "admin/login.html");
      } else if (key === FAMILY_KEY) {
        CK.session.clearFamily();
        window.location.replace(CK.rootPath() + "family/login.html");
      } else {
        CK.session.clearFamily();
        CK.session.clearAdmin();
        window.location.replace(CK.rootPath() + "index.html");
      }
    }
  };

  // ---------------------------------------------------------------------
  // API helper
  // ---------------------------------------------------------------------
  CK.api = {
    /**
     * Low-level call. Attaches the current area's bearer token (unless
     * opts.skipAuth), and on a 401 transparently refreshes the token once
     * and retries the request before giving up. Returns a jQuery-style
     * promise so callers can chain .done(fn) / .fail(fn) / .always(fn).
     */
    call: function (method, url, data, opts) {
      opts = opts || {};
      var ajaxOpts = {
        url: url,
        method: method,
        dataType: "json"
      };
      if (data !== undefined && data !== null) {
        ajaxOpts.contentType = "application/json";
        ajaxOpts.data = JSON.stringify(data);
      }
      if (!opts.skipAuth) {
        var token = CK.session.getActiveAccessToken();
        if (token) {
          ajaxOpts.headers = { Authorization: "Bearer " + token };
        }
      }

      var deferred = $.Deferred();
      $.ajax(ajaxOpts)
        .done(function (resp, status, jqXHR) { deferred.resolve(resp, status, jqXHR); })
        .fail(function (jqXHR) {
          var canRetry = jqXHR.status === 401 && !opts.skipAuth && !opts._isRetry;
          if (!canRetry) {
            deferred.reject(jqXHR);
            return;
          }
          CK.session.refreshActive()
            .done(function () {
              var retryOpts = $.extend({}, opts, { _isRetry: true });
              CK.api.call(method, url, data, retryOpts)
                .done(function (resp, status, retryXHR) { deferred.resolve(resp, status, retryXHR); })
                .fail(function (retryXHR) { deferred.reject(retryXHR); });
            })
            .fail(function () {
              CK.session.handleAuthFailure();
              deferred.reject(jqXHR);
            });
        });
      return deferred.promise();
    },
    get: function (url) { return CK.api.call("GET", url); },
    post: function (url, data) { return CK.api.call("POST", url, data); },
    put: function (url, data) { return CK.api.call("PUT", url, data); },
    del: function (url) { return CK.api.call("DELETE", url); },

    /**
     * Turns a failed jqXHR into a readable message. Handles the three shapes
     * this API can return: a plain quoted JSON string (BadRequest("...")/
     * Conflict("...")), an ASP.NET Core ProblemDetails object (automatic
     * [ApiController] model-validation errors), or no body at all (404).
     */
    errorMessage: function (jqXHR, fallback) {
      if (!jqXHR) {
        return fallback || "Something went wrong.";
      }
      if (jqXHR.status === 0) {
        return "Could not reach the server. Please check that the app is running.";
      }
      if (jqXHR.status === 401) {
        return "Your session has expired. Please sign in again.";
      }
      var body = jqXHR.responseJSON;
      if (body === undefined && jqXHR.responseText) {
        try { body = JSON.parse(jqXHR.responseText); } catch (e) { body = jqXHR.responseText; }
      }
      if (typeof body === "string" && body.trim().length > 0) {
        return body;
      }
      if (body && typeof body === "object") {
        if (body.errors && typeof body.errors === "object") {
          var msgs = [];
          $.each(body.errors, function (field, fieldMsgs) {
            if ($.isArray(fieldMsgs)) {
              msgs = msgs.concat(fieldMsgs);
            }
          });
          if (msgs.length > 0) { return msgs.join(" "); }
        }
        if (body.title) { return body.title; }
        if (body.message) { return body.message; }
      }
      if (jqXHR.status === 404) {
        return fallback || "The record you were looking for was not found.";
      }
      return fallback || ("Request failed (HTTP " + jqXHR.status + ").");
    }
  };

  // ---------------------------------------------------------------------
  // Auth helper -- thin wrappers around /api/auth/*
  // ---------------------------------------------------------------------
  CK.auth = {
    login: function (email, password) {
      return CK.api.call("POST", "/api/auth/login", { email: email, password: password }, { skipAuth: true });
    },
    bootstrapAdmin: function (payload) {
      return CK.api.call("POST", "/api/auth/bootstrap-admin", payload, { skipAuth: true });
    },
    logout: function (refreshToken) {
      return CK.api.call("POST", "/api/auth/logout", { refreshToken: refreshToken }, { skipAuth: true });
    },
    changePassword: function (currentPassword, newPassword) {
      return CK.api.post("/api/auth/change-password", { currentPassword: currentPassword, newPassword: newPassword });
    },
    adminResetPassword: function (userId, newPassword) {
      return CK.api.post("/api/auth/admin-reset-password", { userId: userId, newPassword: newPassword });
    }
  };

  /** Works out the site root ("/" ) from any nesting depth (e.g. /family/, /admin/). */
  CK.rootPath = function () {
    return window.location.pathname.indexOf("/family/") >= 0 || window.location.pathname.indexOf("/admin/") >= 0
      ? "../"
      : "";
  };

  // ---------------------------------------------------------------------
  // Formatting / escaping helpers
  // ---------------------------------------------------------------------
  CK.esc = function (value) {
    if (value === null || value === undefined) { return ""; }
    return String(value)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#39;");
  };

  var WEEKDAY = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
  var MONTH = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

  CK.fmt = {
    /** "2026-09-23" -> "Wed, 23 Sep 2026". Parses the date-only string manually to avoid UTC/local shifting. */
    date: function (isoDate) {
      if (!isoDate) { return ""; }
      var parts = String(isoDate).split("T")[0].split("-");
      if (parts.length !== 3) { return isoDate; }
      var y = parseInt(parts[0], 10), m = parseInt(parts[1], 10), d = parseInt(parts[2], 10);
      var dt = new Date(y, m - 1, d);
      return WEEKDAY[dt.getDay()] + ", " + d + " " + MONTH[m - 1] + " " + y;
    },
    dateShort: function (isoDate) {
      if (!isoDate) { return ""; }
      var parts = String(isoDate).split("T")[0].split("-");
      if (parts.length !== 3) { return isoDate; }
      var y = parseInt(parts[0], 10), m = parseInt(parts[1], 10), d = parseInt(parts[2], 10);
      return d + " " + MONTH[m - 1] + " " + y;
    },
    dateTime: function (isoDateTime) {
      if (!isoDateTime) { return ""; }
      var dt = new Date(isoDateTime);
      if (isNaN(dt.getTime())) { return isoDateTime; }
      var hh = dt.getHours(), mm = dt.getMinutes();
      var ampm = hh >= 12 ? "PM" : "AM";
      hh = hh % 12; if (hh === 0) { hh = 12; }
      var mmStr = mm < 10 ? "0" + mm : String(mm);
      return CK.fmt.dateShort(isoDateTime) + ", " + hh + ":" + mmStr + " " + ampm;
    }
  };

  /** Today as a yyyy-MM-dd string, using the browser's local date. */
  CK.today = function () {
    var d = new Date();
    var mm = d.getMonth() + 1, dd = d.getDate();
    return d.getFullYear() + "-" + (mm < 10 ? "0" + mm : mm) + "-" + (dd < 10 ? "0" + dd : dd);
  };

  CK.addDays = function (isoDate, days) {
    var parts = isoDate.split("-");
    var dt = new Date(parseInt(parts[0], 10), parseInt(parts[1], 10) - 1, parseInt(parts[2], 10));
    dt.setDate(dt.getDate() + days);
    var mm = dt.getMonth() + 1, dd = dt.getDate();
    return dt.getFullYear() + "-" + (mm < 10 ? "0" + mm : mm) + "-" + (dd < 10 ? "0" + dd : dd);
  };

  // ---------------------------------------------------------------------
  // Toasts
  // ---------------------------------------------------------------------
  CK.toast = function (message, type) {
    type = type || "success";
    var iconByType = {
      success: "bi-check-circle-fill",
      danger: "bi-exclamation-triangle-fill",
      warning: "bi-exclamation-circle-fill",
      info: "bi-info-circle-fill"
    };
    var $container = $("#ckToastContainer");
    if ($container.length === 0) {
      $container = $('<div id="ckToastContainer" class="toast-container position-fixed top-0 end-0 p-3" style="z-index:1080"></div>');
      $("body").append($container);
    }
    var $toast = $(
      '<div class="toast align-items-center text-bg-' + CK.esc(type) + ' border-0" role="alert" aria-live="assertive" aria-atomic="true">' +
        '<div class="d-flex">' +
          '<div class="toast-body"><i class="bi ' + (iconByType[type] || iconByType.info) + ' me-2"></i>' + CK.esc(message) + '</div>' +
          '<button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>' +
        '</div>' +
      '</div>'
    );
    $container.append($toast);
    var toast = new bootstrap.Toast($toast[0], { delay: 4500 });
    toast.show();
    $toast.on("hidden.bs.toast", function () { $toast.remove(); });
  };

  // ---------------------------------------------------------------------
  // Shared navbar
  // ---------------------------------------------------------------------
  CK.nav = {
    /**
     * role: "family" | "admin" | "guest"
     * active: top-level key to highlight ("dashboard" | "families" | "kitchen" | "reports")
     */
    render: function (role, active) {
      var root = CK.rootPath();
      var $mount = $("#ckNavbar");
      if ($mount.length === 0) { return; }

      var brand = '<a class="navbar-brand" href="' + root + 'index.html"><i class="bi bi-basket2-fill"></i> Faiz al-Mawaid al-Burhaniyah</a>';
      var links = "";
      var userChip = "";

      function link(href, key, label) {
        var isActive = key === active;
        return '<li class="nav-item"><a class="nav-link' + (isActive ? " active" : "") + '" href="' + href + '">' + label + "</a></li>";
      }

      if (role === "family") {
        var fs = CK.session.getFamily();
        links += link(root + "family/dashboard.html", "dashboard", "My Thaali");
        links += link(root + "family/dashboard.html#feedback", "feedback", "Feedback");
        if (fs) {
          userChip =
            '<div class="dropdown">' +
              '<button class="btn btn-outline-primary btn-sm dropdown-toggle" type="button" data-bs-toggle="dropdown">' +
                '<i class="bi bi-person-fill"></i> ' + CK.esc(fs.fullName || fs.email) +
              '</button>' +
              '<ul class="dropdown-menu dropdown-menu-end">' +
                '<li><a class="dropdown-item" href="#" id="ckChangePasswordLink"><i class="bi bi-key me-2"></i>Change Password</a></li>' +
                '<li><hr class="dropdown-divider"></li>' +
                '<li><a class="dropdown-item text-danger" href="#" id="ckLogoutBtn"><i class="bi bi-box-arrow-right me-2"></i>Logout</a></li>' +
              '</ul>' +
            '</div>';
        }
      } else if (role === "admin") {
        var as = CK.session.getAdmin();
        links += link(root + "admin/dashboard.html", "dashboard", "Dashboard");
        links +=
          '<li class="nav-item dropdown">' +
            '<a class="nav-link dropdown-toggle' + (active === "families" ? " active" : "") + '" href="#" role="button" data-bs-toggle="dropdown">Families</a>' +
            '<ul class="dropdown-menu">' +
              '<li><a class="dropdown-item" href="' + root + 'admin/registrations.html"><i class="bi bi-person-check me-2"></i>Pending Registrations</a></li>' +
              '<li><a class="dropdown-item" href="' + root + 'admin/families.html"><i class="bi bi-people me-2"></i>All Families</a></li>' +
              '<li><a class="dropdown-item" href="' + root + 'admin/users.html"><i class="bi bi-person-badge me-2"></i>Users &amp; Passwords</a></li>' +
              '<li><a class="dropdown-item" href="' + root + 'admin/feedback.html"><i class="bi bi-chat-left-text me-2"></i>Family Feedback <span id="ckFeedbackBadge" class="badge rounded-pill text-bg-danger ms-1 d-none"></span></a></li>' +
              '<li><a class="dropdown-item" href="' + root + 'admin/address-change-requests.html"><i class="bi bi-house-gear me-2"></i>Address Change Requests <span id="ckAddressChangeBadge" class="badge rounded-pill text-bg-danger ms-1 d-none"></span></a></li>' +
              '<li><a class="dropdown-item" href="' + root + 'admin/thaali-size-change-requests.html"><i class="bi bi-box-seam me-2"></i>Thaali Size Change Requests <span id="ckSizeChangeBadge" class="badge rounded-pill text-bg-danger ms-1 d-none"></span></a></li>' +
            '</ul>' +
          '</li>' +
          '<li class="nav-item dropdown">' +
            '<a class="nav-link dropdown-toggle' + (active === "kitchen" ? " active" : "") + '" href="#" role="button" data-bs-toggle="dropdown">Kitchen Setup</a>' +
            '<ul class="dropdown-menu">' +
              '<li><a class="dropdown-item" href="' + root + 'admin/meal-calendar.html"><i class="bi bi-calendar3 me-2"></i>Meal Calendar</a></li>' +
              '<li><a class="dropdown-item" href="' + root + 'admin/meal-plans.html"><i class="bi bi-journal-text me-2"></i>Meal Plans</a></li>' +
              '<li><a class="dropdown-item" href="' + root + 'admin/meal-plan-templates.html"><i class="bi bi-collection me-2"></i>Meal Plan Templates</a></li>' +
              '<li><a class="dropdown-item" href="' + root + 'admin/thaali-sizes.html"><i class="bi bi-box-seam me-2"></i>Thaali Sizes</a></li>' +
              '<li><a class="dropdown-item" href="' + root + 'admin/areas.html"><i class="bi bi-signpost-split me-2"></i>Areas</a></li>' +
              '<li><a class="dropdown-item" href="' + root + 'admin/delivery-persons.html"><i class="bi bi-bicycle me-2"></i>Delivery Persons</a></li>' +
              '<li><a class="dropdown-item" href="' + root + 'admin/non-serving-days.html"><i class="bi bi-calendar-x me-2"></i>Non-Serving Days</a></li>' +
              '<li><a class="dropdown-item" href="' + root + 'admin/settings.html"><i class="bi bi-gear me-2"></i>App Settings</a></li>' +
            '</ul>' +
          '</li>' +
          '<li class="nav-item dropdown">' +
            '<a class="nav-link dropdown-toggle' + (active === "reports" ? " active" : "") + '" href="#" role="button" data-bs-toggle="dropdown">Reports</a>' +
            '<ul class="dropdown-menu">' +
              '<li><a class="dropdown-item" href="' + root + 'admin/reports.html"><i class="bi bi-bar-chart me-2"></i>Daily Thaali Count</a></li>' +
              '<li><a class="dropdown-item" href="' + root + 'admin/audit-log.html"><i class="bi bi-clock-history me-2"></i>Audit Log</a></li>' +
            '</ul>' +
          '</li>';
        if (as) {
          userChip =
            '<div class="dropdown">' +
              '<button class="btn btn-outline-primary btn-sm dropdown-toggle" type="button" data-bs-toggle="dropdown">' +
                '<i class="bi bi-shield-check"></i> ' + CK.esc(as.fullName || as.email) +
              '</button>' +
              '<ul class="dropdown-menu dropdown-menu-end">' +
                '<li><a class="dropdown-item" href="#" id="ckChangePasswordLink"><i class="bi bi-key me-2"></i>Change Password</a></li>' +
                '<li><hr class="dropdown-divider"></li>' +
                '<li><a class="dropdown-item text-danger" href="#" id="ckLogoutBtn"><i class="bi bi-box-arrow-right me-2"></i>Logout</a></li>' +
              '</ul>' +
            '</div>';
        }
      }

      var html =
        '<nav class="navbar navbar-expand-lg ck-navbar sticky-top py-2">' +
          '<div class="container">' +
            brand +
            '<button class="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#ckNavCollapse">' +
              '<span class="navbar-toggler-icon"></span>' +
            '</button>' +
            '<div class="collapse navbar-collapse" id="ckNavCollapse">' +
              '<ul class="navbar-nav me-auto mb-2 mb-lg-0">' + links + '</ul>' +
              '<div class="d-flex align-items-center">' + userChip + '</div>' +
            '</div>' +
          '</div>' +
        '</nav>';

      $mount.html(html);

      $("#ckLogoutBtn").on("click", function (e) {
        e.preventDefault();
        if (role === "family") { CK.session.clearFamily(); }
        if (role === "admin") { CK.session.clearAdmin(); }
        window.location.href = root + "index.html";
      });

      $("#ckChangePasswordLink").on("click", function (e) {
        e.preventDefault();
        CK.changePassword.open(false);
      });

      // Small "waiting for a response" badge on the Feedback nav link -- best-effort,
      // fails silently (e.g. on pages that render the navbar before the token is ready).
      if (role === "admin") {
        CK.api.get("/api/feedback/open-count").done(function (count) {
          if (count > 0) {
            $("#ckFeedbackBadge").text(count).removeClass("d-none");
          }
        });
        CK.api.get("/api/addresschangerequests/pending-count").done(function (count) {
          if (count > 0) {
            $("#ckAddressChangeBadge").text(count).removeClass("d-none");
          }
        });
        CK.api.get("/api/thaalisizechangerequests/pending-count").done(function (count) {
          if (count > 0) {
            $("#ckSizeChangeBadge").text(count).removeClass("d-none");
          }
        });
      }

      // A password an Admin set on someone's behalf must be changed before they go any further.
      if (role === "family" && fs && fs.mustChangePassword) {
        CK.changePassword.open(true);
      } else if (role === "admin" && as && as.mustChangePassword) {
        CK.changePassword.open(true);
      }
    }
  };

  // ---------------------------------------------------------------------
  // Change Password modal (shared across family + admin areas)
  // ---------------------------------------------------------------------
  CK.changePassword = {
    ensureModal: function () {
      if ($("#ckChangePasswordModal").length > 0) { return; }
      var html =
        '<div class="modal fade" id="ckChangePasswordModal" tabindex="-1" aria-hidden="true">' +
          '<div class="modal-dialog">' +
            '<div class="modal-content">' +
              '<div class="modal-header">' +
                '<h5 class="modal-title"><i class="bi bi-key-fill me-2"></i>Change Password</h5>' +
                '<button type="button" class="btn-close" id="ckCpCloseBtn" data-bs-dismiss="modal" aria-label="Close"></button>' +
              '</div>' +
              '<div class="modal-body">' +
                '<div id="ckCpForcedNotice" class="alert alert-warning d-none"><i class="bi bi-exclamation-triangle-fill me-1"></i> Your password was set by an Admin. Please choose a new password to continue.</div>' +
                '<div id="ckCpAlert" class="alert alert-danger d-none"></div>' +
                '<form id="ckCpForm" novalidate>' +
                  '<div class="mb-3">' +
                    '<label class="form-label" for="ckCpCurrent">Current password</label>' +
                    '<input type="password" class="form-control" id="ckCpCurrent" required>' +
                  '</div>' +
                  '<div class="mb-3">' +
                    '<label class="form-label" for="ckCpNew">New password</label>' +
                    '<input type="password" class="form-control" id="ckCpNew" required minlength="8">' +
                    '<div class="form-text">At least 8 characters.</div>' +
                  '</div>' +
                  '<div class="mb-3">' +
                    '<label class="form-label" for="ckCpConfirm">Confirm new password</label>' +
                    '<input type="password" class="form-control" id="ckCpConfirm" required minlength="8">' +
                  '</div>' +
                '</form>' +
              '</div>' +
              '<div class="modal-footer">' +
                '<button type="button" class="btn btn-outline-secondary" id="ckCpCancelBtn" data-bs-dismiss="modal">Cancel</button>' +
                '<button type="button" class="btn btn-primary" id="ckCpSubmitBtn"><i class="bi bi-check2 me-1"></i>Update Password</button>' +
              '</div>' +
            '</div>' +
          '</div>' +
        '</div>';
      $("body").append(html);

      $("#ckCpSubmitBtn").on("click", function () {
        $("#ckCpAlert").addClass("d-none");
        var current = $("#ckCpCurrent").val();
        var next = $("#ckCpNew").val();
        var confirmVal = $("#ckCpConfirm").val();

        if (!current || next.length < 8) {
          $("#ckCpAlert").text("Please fill in your current password and a new password of at least 8 characters.").removeClass("d-none");
          return;
        }
        if (next !== confirmVal) {
          $("#ckCpAlert").text("New password and confirmation do not match.").removeClass("d-none");
          return;
        }

        var $btn = $(this).prop("disabled", true).html('<span class="spinner-border spinner-border-sm me-1"></span>Updating&hellip;');
        CK.auth.changePassword(current, next).done(function () {
          // Reflect locally right away so the forced prompt doesn't reappear this session.
          var fs = CK.session.getFamily();
          if (fs) { fs.mustChangePassword = false; CK.session.setFamily(fs); }
          var as = CK.session.getAdmin();
          if (as) { as.mustChangePassword = false; CK.session.setAdmin(as); }

          CK.toast("Your password has been updated.", "success");
          $("#ckCpForm")[0].reset();
          CK.changePassword.setForced(false);
          var modalEl = document.getElementById("ckChangePasswordModal");
          var modal = bootstrap.Modal.getInstance(modalEl);
          if (modal) { modal.hide(); }
        }).fail(function (xhr) {
          $("#ckCpAlert").text(CK.api.errorMessage(xhr, "Could not update your password.")).removeClass("d-none");
        }).always(function () {
          $btn.prop("disabled", false).html('<i class="bi bi-check2 me-1"></i>Update Password');
        });
      });

      $("#ckChangePasswordModal").on("hidden.bs.modal", function () {
        $("#ckCpForm")[0].reset();
        $("#ckCpAlert").addClass("d-none");
      });
    },

    /** forced=true removes the close/cancel affordances and shows the "your password was set by an Admin" notice. */
    setForced: function (forced) {
      $("#ckCpCloseBtn").toggleClass("d-none", !!forced);
      $("#ckCpCancelBtn").toggleClass("d-none", !!forced);
      $("#ckCpForcedNotice").toggleClass("d-none", !forced);
    },

    open: function (forced) {
      CK.changePassword.ensureModal();
      CK.changePassword.setForced(!!forced);
      var modalEl = document.getElementById("ckChangePasswordModal");
      var existing = bootstrap.Modal.getInstance(modalEl);
      if (existing) { existing.dispose(); }
      var modal = new bootstrap.Modal(modalEl, { backdrop: forced ? "static" : true, keyboard: !forced });
      modal.show();
    }
  };

  // ---------------------------------------------------------------------
  // Excel helpers (bulk upload/download templates) -- assumes the SheetJS
  // <script> (xlsx.full.min.js) is loaded on whichever page calls these; it's
  // not loaded globally in site.js itself, to keep pages that don't need it light.
  // ---------------------------------------------------------------------
  CK.excel = {
    /**
     * Builds and downloads a .xlsx file with one data sheet (plus an optional
     * "Instructions" sheet) from an array of plain row objects. `headers` is
     * an ordered array of {key, label, width?} so column order/labels are
     * explicit rather than relying on object key order.
     */
    downloadTemplate: function (filename, sheetName, headers, rows, instructionLines) {
      var aoa = [headers.map(function (h) { return h.label; })];
      (rows || []).forEach(function (row) {
        aoa.push(headers.map(function (h) { return row[h.key] !== undefined && row[h.key] !== null ? row[h.key] : ""; }));
      });
      var sheet = XLSX.utils.aoa_to_sheet(aoa);
      sheet["!cols"] = headers.map(function (h) { return { wch: h.width || 20 }; });

      var wb = XLSX.utils.book_new();
      XLSX.utils.book_append_sheet(wb, sheet, sheetName || "Sheet1");

      if (instructionLines && instructionLines.length > 0) {
        var instrSheet = XLSX.utils.aoa_to_sheet(instructionLines.map(function (line) { return [line]; }));
        instrSheet["!cols"] = [{ wch: 90 }];
        XLSX.utils.book_append_sheet(wb, instrSheet, "Instructions");
      }

      XLSX.writeFile(wb, filename);
    },

    /**
     * Reads the first sheet of an uploaded .xlsx/.xls/.csv File into an array of
     * plain row objects keyed by `headers[].key`, matched to columns by header
     * text (case-insensitive, trimmed) rather than position, so column order in
     * the uploaded file doesn't have to match the template exactly. Blank rows
     * (every cell empty) are skipped. Calls back(err, rows).
     */
    readRows: function (file, headers, callback) {
      var reader = new FileReader();
      reader.onerror = function () { callback("Could not read that file."); };
      reader.onload = function (e) {
        try {
          var data = new Uint8Array(e.target.result);
          var wb = XLSX.read(data, { type: "array", cellDates: true });
          var sheet = wb.Sheets[wb.SheetNames[0]];
          var aoa = XLSX.utils.sheet_to_json(sheet, { header: 1, defval: "" });
          if (aoa.length === 0) { callback(null, []); return; }

          var headerRow = aoa[0].map(function (h) { return String(h || "").trim().toLowerCase(); });
          var colIndex = {};
          headers.forEach(function (h) { colIndex[h.key] = headerRow.indexOf(h.label.toLowerCase()); });

          var rows = [];
          for (var i = 1; i < aoa.length; i++) {
            var raw = aoa[i];
            var isBlank = !raw || raw.every(function (v) { return v === "" || v === undefined || v === null; });
            if (isBlank) { continue; }

            var row = {};
            headers.forEach(function (h) {
              var idx = colIndex[h.key];
              row[h.key] = idx >= 0 && idx < raw.length ? raw[idx] : "";
            });
            rows.push(row);
          }
          callback(null, rows);
        } catch (ex) {
          callback("That doesn't look like a valid Excel file.");
        }
      };
      reader.readAsArrayBuffer(file);
    },

    /** A cell value that's either a real Excel date (a JS Date, read with cellDates:true) or plain "YYYY-MM-DD" text -- returns an ISO date string, or null if neither. */
    parseDateCell: function (raw) {
      if (raw instanceof Date && !isNaN(raw.getTime())) {
        var y = raw.getUTCFullYear(), m = raw.getUTCMonth() + 1, d = raw.getUTCDate();
        return y + "-" + (m < 10 ? "0" : "") + m + "-" + (d < 10 ? "0" : "") + d;
      }
      var s = String(raw || "").trim();
      return /^\d{4}-\d{2}-\d{2}$/.test(s) ? s : null;
    }
  };

  window.CK = CK;
})(window, jQuery);
