(function () {
  const sidebarToggle = document.getElementById("sidebarToggle");
  const appSidebar = document.getElementById("appSidebar");

  if (sidebarToggle && appSidebar) {
    sidebarToggle.addEventListener("click", function () {
      appSidebar.classList.toggle("show");
    });
  }

  const path = window.location.pathname.toLowerCase().replace(/\/+$/, "") || "/";
  const links = Array.from(document.querySelectorAll(".nav-menu .nav-link"));
  links.forEach((link) => link.classList.remove("active"));
  links.sort((a, b) => (b.getAttribute("href") || "").length - (a.getAttribute("href") || "").length);

  const normalize = (value) => {
    const clean = (value || "").split("?")[0].split("#")[0].toLowerCase().replace(/\/+$/, "");
    return clean || "/";
  };

  const active = links.find((link) => {
    const href = normalize(link.getAttribute("href") || "");
    if (!href) return false;
    if (href === "/" || href === "/index") return path === "/" || path === "/index";
    return path === href || path.startsWith(`${href}/`);
  });

  if (active) {
    active.classList.add("active");
  }

  document.querySelectorAll(".config-menu").forEach((menu) => {
    const pinned = (menu.getAttribute("data-pinned") || "").toLowerCase() === "true";
    if (pinned) {
      menu.setAttribute("open", "");
    }
  });

  window.paintCategoryPills = function () {
    const palette = [
      ["#dbeafe", "#1d4ed8", "#93c5fd"],
      ["#dcfce7", "#166534", "#86efac"],
      ["#fef3c7", "#92400e", "#fcd34d"],
      ["#fee2e2", "#991b1b", "#fca5a5"],
      ["#e0f2fe", "#0e7490", "#7dd3fc"],
      ["#ede9fe", "#5b21b6", "#c4b5fd"],
      ["#fce7f3", "#9d174d", "#f9a8d4"],
      ["#ecfccb", "#3f6212", "#bef264"],
      ["#ffedd5", "#9a3412", "#fdba74"]
    ];

    document.querySelectorAll(".category-pill").forEach((pill) => {
      const key = (pill.getAttribute("data-category") || pill.textContent || "").trim().toLowerCase();
      const hash = key.split("").reduce((acc, ch) => acc + ch.charCodeAt(0), 0);
      const [bg, fg, bd] = palette[hash % palette.length];
      pill.style.backgroundColor = bg;
      pill.style.color = fg;
      pill.style.borderColor = bd;
    });
  };

  window.paintCategoryPills();

  const posForm = document.getElementById("posForm");
  if (!posForm) {
    return;
  }

  const currencySymbol = posForm.getAttribute("data-currency") || "$";
  function toMoney(value) {
    const num = Number(value || 0);
    return `${currencySymbol}${num.toLocaleString("es-MX", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  }

  const paymentMethod = document.getElementById("paymentMethod");
  const amountReceived = document.getElementById("amountReceived");
  const globalDiscountPercent = document.getElementById("globalDiscountPercent");
  const pricesIncludeTax = document.getElementById("pricesIncludeTax");
  const searchInput = document.getElementById("productSearch");
  const barcodeInput = document.getElementById("barcodeInput");
  const cartRows = document.getElementById("cartRows");

  const taxRate = parseFloat(posForm.getAttribute("data-tax") || "16") / 100;

  function getCards() {
    return Array.from(document.querySelectorAll(".product-card[data-product-id]"));
  }

  function getQtyInput(card) {
    return card.querySelector(".qty-input");
  }

  function getLineDiscountInput(card) {
    return card.querySelector(".line-discount-input");
  }

  function normalizePaymentFields() {
    const isCash = (paymentMethod?.value || "Cash") === "Cash";
    if (amountReceived) {
      amountReceived.disabled = !isCash;
      if (!isCash && !amountReceived.value) {
        amountReceived.value = "0";
      }
    }
  }

  function renderCart(rows) {
    if (!cartRows) return;

    if (rows.length === 0) {
      cartRows.innerHTML = '<p class="text-muted mb-0">No hay productos en carrito.</p>';
      return;
    }

    cartRows.innerHTML = rows
      .map((r) => {
        const discLabel = r.discountPercent > 0 ? ` <span class="badge-soft badge-warn">-${r.discountPercent.toFixed(2)}%</span>` : "";
        const promoLabel = r.promoAmount > 0 ? ` <span class="badge-soft badge-info">Promo</span>` : "";
        return `<div class="ticket-row"><span>${r.name} x ${r.qty}${promoLabel}${discLabel}</span><strong>${toMoney(r.total)}</strong></div>`;
      })
      .join("");
  }

  function promoDiscountFor(card, qty, price) {
    const type = card.getAttribute("data-promo-type") || "None";
    const promoValue = parseFloat(card.getAttribute("data-promo-value") || "0") || 0;
    const promoMin = parseInt(card.getAttribute("data-promo-min") || "0", 10);

    if (qty <= 0 || price <= 0) return 0;

    if (type === "TwoForOne") {
      return Math.floor(qty / 2) * price;
    }

    if (type === "Volume" && promoMin > 0 && qty >= promoMin && promoValue > 0) {
      return (price * qty) * (promoValue / 100);
    }

    return 0;
  }

  function refreshPosSummary() {
    const cards = getCards();
    const selectedRows = [];

    let gross = 0;
    let discountAmount = 0;
    let items = 0;

    cards.forEach((card) => {
      const price = parseFloat(card.getAttribute("data-price") || "0");
      const name = card.querySelector(".product-name")?.textContent?.trim() || "Producto";
      const qty = parseInt(getQtyInput(card)?.value || "0", 10);
      const lineDiscPercent = parseFloat(getLineDiscountInput(card)?.value || "0") || 0;

      if (qty > 0) {
        const lineGross = price * qty;
        const promoDiscount = promoDiscountFor(card, qty, price);
        const lineDisc = (lineGross - promoDiscount) * (Math.max(0, Math.min(100, lineDiscPercent)) / 100);
        const lineTotal = lineGross - promoDiscount - lineDisc;

        gross += lineGross;
        discountAmount += lineDisc + promoDiscount;
        items += qty;

        selectedRows.push({ name, qty, discountPercent: lineDiscPercent, promoAmount: promoDiscount, total: lineTotal });
      }
    });

    const globalDiscPercent = Math.max(0, Math.min(100, parseFloat(globalDiscountPercent?.value || "0") || 0));
    const globalDiscAmount = (gross - discountAmount) * (globalDiscPercent / 100);

    const discounted = gross - discountAmount - globalDiscAmount;
    const includesTax = pricesIncludeTax?.checked === true;
    const subtotal = includesTax ? (taxRate > 0 ? discounted / (1 + taxRate) : discounted) : discounted;
    const iva = includesTax ? discounted - subtotal : subtotal * taxRate;
    const total = includesTax ? discounted : subtotal + iva;

    const received = parseFloat(amountReceived?.value || "0") || 0;
    const change = (paymentMethod?.value || "Cash") === "Cash" ? received - total : 0;

    document.getElementById("posItems").textContent = String(items);
    document.getElementById("posSubtotal").textContent = toMoney(subtotal);
    document.getElementById("posDiscount").textContent = toMoney(discountAmount + globalDiscAmount);
    document.getElementById("posIva").textContent = toMoney(iva);
    document.getElementById("posTotal").textContent = toMoney(total);
    document.getElementById("posReceived").textContent = toMoney(received);
    document.getElementById("posChange").textContent = toMoney(change);

    renderCart(selectedRows);
  }

  function attachQtyButtons() {
    document.querySelectorAll(".btn-inc").forEach((btn) => {
      btn.addEventListener("click", () => {
        const card = btn.closest(".product-card");
        const input = getQtyInput(card);
        if (!input) return;
        const max = parseInt(input.max || "999", 10);
        const val = parseInt(input.value || "0", 10);
        input.value = String(Math.min(max, val + 1));
        refreshPosSummary();
      });
    });

    document.querySelectorAll(".btn-dec").forEach((btn) => {
      btn.addEventListener("click", () => {
        const card = btn.closest(".product-card");
        const input = getQtyInput(card);
        if (!input) return;
        const val = parseInt(input.value || "0", 10);
        input.value = String(Math.max(0, val - 1));
        refreshPosSummary();
      });
    });
  }

  function attachInputs() {
    document.querySelectorAll(".qty-input, .line-discount-input").forEach((input) => {
      input.addEventListener("input", refreshPosSummary);
    });

    amountReceived?.addEventListener("input", refreshPosSummary);
    globalDiscountPercent?.addEventListener("input", refreshPosSummary);
    pricesIncludeTax?.addEventListener("change", refreshPosSummary);

    paymentMethod?.addEventListener("change", () => {
      normalizePaymentFields();
      refreshPosSummary();
    });
  }

  function increaseByBarcode(code) {
    const q = (code || "").trim().toLowerCase();
    if (!q) {
      barcodeInput?.focus();
      return;
    }

    const card = getCards().find(c => (c.getAttribute("data-code") || "") === q);
    if (!card) {
      barcodeInput?.focus();
      return;
    }

    const input = getQtyInput(card);
    if (!input) {
      barcodeInput?.focus();
      return;
    }

    const max = parseInt(input.max || "999", 10);
    const val = parseInt(input.value || "0", 10);
    input.value = String(Math.min(max, val + 1));
    refreshPosSummary();
    setTimeout(() => {
      barcodeInput?.focus();
      barcodeInput?.select();
    }, 0);
  }

  function attachSearchAndHotkeys() {
    if (barcodeInput) {
      barcodeInput.addEventListener("keydown", (e) => {
        if (e.key === "Enter") {
          e.preventDefault();
          increaseByBarcode(barcodeInput.value);
          barcodeInput.value = "";
          barcodeInput.focus();
        }
      });
      setTimeout(() => barcodeInput.focus(), 0);
    }

    if (searchInput) {
      searchInput.addEventListener("input", () => {
        const q = searchInput.value.trim().toLowerCase();
        getCards().forEach((card) => {
          const name = card.getAttribute("data-name") || "";
          const code = card.getAttribute("data-code") || "";
          const visible = !q || name.includes(q) || code.includes(q);
          card.style.display = visible ? "block" : "none";
        });
      });
    }

    document.addEventListener("keydown", (e) => {
      if (e.key === "F2") {
        e.preventDefault();
        searchInput?.focus();
        searchInput?.select();
      }

      if (e.key === "F4") {
        e.preventDefault();
        document.getElementById("btnCharge")?.click();
      }

      if (e.key === "+" || e.key === "-") {
        const activeEl = document.activeElement;
        if (activeEl && activeEl.classList.contains("qty-input")) {
          e.preventDefault();
          const input = activeEl;
          const max = parseInt(input.max || "999", 10);
          const val = parseInt(input.value || "0", 10);
          input.value = e.key === "+" ? String(Math.min(max, val + 1)) : String(Math.max(0, val - 1));
          refreshPosSummary();
        }
      }
    });
  }

  normalizePaymentFields();
  attachQtyButtons();
  attachInputs();
  attachSearchAndHotkeys();
  refreshPosSummary();
})();

