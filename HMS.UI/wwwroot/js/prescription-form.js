
(function () {
    console.log("Prescription JS loaded");
    function parseInventoryItems() {
        const element = document.getElementById("prescription-inventory-data");
        if (!element) {
            return [];
        }

        try {
            return JSON.parse(element.textContent || "[]");
        } catch {
            return [];
        }
    }

    function escapeHtml(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function buildInventoryOptions(inventoryItems, selectedId) {
        const otherSelected = !selectedId ? ' selected="selected"' : "";
        const options = [`<option value=""${otherSelected}>Other (Not in Inventory)</option>`];

        inventoryItems.forEach(function (item) {
            const isSelected = selectedId && selectedId.toLowerCase() === String(item.id).toLowerCase()
                ? ' selected="selected"'
                : "";
            options.push(`<option value="${escapeHtml(String(item.id))}"${isSelected}>${escapeHtml(item.name)} (${escapeHtml(item.code)})</option>`);
        });

        return options.join("");
    }

    function updateMedicationInput(selectElement) {
        const row = selectElement.closest("[data-prescription-item-row]");
        if (!row) {
            return;
        }

        const medicationInput = row.querySelector("[data-medication-name]");
        if (!medicationInput) {
            return;
        }

        const selectedOption = selectElement.options[selectElement.selectedIndex];
        const selectedText = selectedOption ? selectedOption.text : "";

        if (selectElement.value) {
            const cutIndex = selectedText.lastIndexOf(" (");
            medicationInput.value = cutIndex > 0 ? selectedText.substring(0, cutIndex) : selectedText;
            medicationInput.setAttribute("readonly", "readonly");
        } else {
            medicationInput.removeAttribute("readonly");
            if (!medicationInput.dataset.userTyped) {
                medicationInput.value = "";
            }
        }
    }

    function wireRow(row) {
        const select = row.querySelector("[data-inventory-select]");
        const medicationInput = row.querySelector("[data-medication-name]");
        const removeButton = row.querySelector("[data-remove-prescription-item]");

        if (medicationInput) {
            medicationInput.addEventListener("input", function () {
                if (!medicationInput.hasAttribute("readonly")) {
                    medicationInput.dataset.userTyped = medicationInput.value ? "1" : "";
                }
            });
        }

        if (select) {
            select.addEventListener("change", function () {
                updateMedicationInput(select);
            });
            updateMedicationInput(select);
        }

        if (removeButton) {
            removeButton.addEventListener("click", function () {
                const container = row.parentElement;
                row.remove();
                if (container) {
                    renumberRows(container);
                }
            });
        }
    }

    function renumberRows(container) {
        const rows = container.querySelectorAll("[data-prescription-item-row]");
        rows.forEach(function (row, index) {
            const title = row.querySelector("h6");
            if (title) {
                title.textContent = `Medication Item ${index + 1}`;
            }

            row.querySelectorAll("[name]").forEach(function (field) {
                field.name = field.name.replace(/Items\[\d+\]/, `Items[${index}]`);
            });
        });
    }

    function addRow(container, inventoryItems) {
        const index = container.querySelectorAll("[data-prescription-item-row]").length;
        const wrapper = document.createElement("div");
        wrapper.className = "col-12 col-xl-6";
        wrapper.setAttribute("data-prescription-item-row", "");
        wrapper.innerHTML = `
<div class="card border-0 shadow-sm h-100">
  <div class="card-body">
    <div class="d-flex justify-content-between align-items-center mb-3">
      <h6 class="mb-0">Medication Item ${index + 1}</h6>
      <div class="d-flex gap-2 align-items-center">
        <span class="badge text-bg-light">Optional stock link</span>
        <button type="button" class="btn btn-outline-danger btn-sm" data-remove-prescription-item>
          <i class="bi bi-trash"></i>
        </button>
      </div>
    </div>
    <div class="row g-3">
      <div class="col-md-6">
        <label class="form-label">Linked inventory item</label>
        <select name="Items[${index}].InventoryItemId" class="form-select" data-inventory-select>
          ${buildInventoryOptions(inventoryItems, "")}
        </select>
      </div>
      <div class="col-md-6">
        <label class="form-label">Medication name</label>
       <input name="Items[${index}].MedicationName" class="form-control" data-medication-name placeholder="e.g. Augmentin 625mg" />
      </div>
      <div class="col-md-3">
        <label class="form-label">Dosage</label>
        <input name="Items[${index}].Dosage" class="form-control" placeholder="1 tab" />
      </div>
      <div class="col-md-3">
        <label class="form-label">Frequency</label>
        <input name="Items[${index}].Frequency" class="form-control" placeholder="BID x 5 days" />
      </div>
      <div class="col-md-3">
        <label class="form-label">Quantity</label>
        <input name="Items[${index}].Quantity" value="1" type="number" min="0" class="form-control" />
      </div>
  <div class="col-md-3">
                <label class="form-label">Billing</label>
                <div class="form-check">
                    <input class="form-check-input" type="checkbox" name="Items[${index}].ChargeSeparately" value="true" @(Model.Item.InventoryItemId.HasValue ? "" : "") />
                    <label class="form-check-label">Charge separately</label>
                </div>
            </div>
    </div>
  </div>
</div>`;

        container.appendChild(wrapper);
        wireRow(wrapper);
    }

    function initializePatientVisitSelector(form) {
        const patientSelect = form.querySelector("[data-prescription-patient]");
        const visitSelect = form.querySelector("[data-prescription-visit]");
        if (!patientSelect || !visitSelect) {
            return;
        }

        const endpoint = visitSelect.dataset.patientVisitsUrl;
        if (!endpoint || patientSelect.disabled) {
            return;
        }

        patientSelect.addEventListener("change", function () {
            visitSelect.innerHTML = '<option value="">Select visit</option>';

            if (!patientSelect.value) {
                return;
            }

            fetch(`${endpoint}?patientId=${encodeURIComponent(patientSelect.value)}`, {
                headers: { "X-Requested-With": "XMLHttpRequest" }
            })
                .then(function (response) {
                    if (!response.ok) {
                        throw new Error("Could not load visits.");
                    }

                    return response.json();
                })
                .then(function (visits) {
                    (visits || []).forEach(function (visit) {
                        const option = document.createElement("option");
                        option.value = visit.id;
                        option.textContent = visit.displayName;
                        visitSelect.appendChild(option);
                    });
                })
                .catch(function () {
                    visitSelect.innerHTML = '<option value="">Unable to load visits</option>';
                });
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        const form = document.querySelector("[data-prescription-form]");
        if (!form) {
            return;
        }

        const inventoryItems = parseInventoryItems();
        const container = form.querySelector("[data-prescription-items-container]");
        const addButton = form.querySelector("[data-add-prescription-item]");

        if (container) {
            container.querySelectorAll("[data-prescription-item-row]").forEach(wireRow);
        }

        if (container && addButton) {
            addButton.addEventListener("click", function () {
                addRow(container, inventoryItems);
            });
        }

        initializePatientVisitSelector(form);
    });
})();
