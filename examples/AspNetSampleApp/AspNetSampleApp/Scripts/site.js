"use strict";

// The page works without JavaScript. Everything in here is an enhancement,
// which is why the related controls start out hidden in the markup.

function setUpEnvironmentFilter() {
	const container = document.getElementById("environment-filter");
	if (!container)
		return;

	const search = document.getElementById("environment-search");
	const count = document.getElementById("environment-count");
	const rows = Array.from(document.querySelectorAll("#environment-table tbody tr"));

	const apply = () => {
		const term = search.value.trim().toLowerCase();
		let visible = 0;

		for (const row of rows) {
			const matches = row.textContent.toLowerCase().includes(term);
			row.hidden = !matches;
			if (matches)
				visible++;
		}

		count.textContent = term ? `${visible} of ${rows.length}` : `${rows.length} variables`;
	};

	search.addEventListener("input", apply);
	container.hidden = false;
	apply();
}

function setUpCopyButtons() {
	// The clipboard API is only available in secure contexts (HTTPS or localhost).
	if (!navigator.clipboard)
		return;

	for (const button of document.querySelectorAll("[data-copy]")) {
		const target = document.querySelector(button.dataset.copy);
		const label = button.textContent;
		let timer;

		button.addEventListener("click", async () => {
			try {
				await navigator.clipboard.writeText(target.textContent);
				button.textContent = "Copied";
			} catch {
				button.textContent = "Copy failed";
			}

			clearTimeout(timer);
			timer = setTimeout(() => { button.textContent = label; }, 1500);
		});

		button.hidden = false;
	}
}

setUpEnvironmentFilter();
setUpCopyButtons();
