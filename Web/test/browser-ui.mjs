import assert from "node:assert/strict";
export async function click(page, selector) {
  const target = page.locator(selector).first();
  await target.scrollIntoViewIfNeeded();
  const accessible = await target.evaluate((button) => {
    const r = button.getBoundingClientRect(),
      hit = document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2);
    return (
      r.width > 0 &&
      r.height > 0 &&
      r.left >= 0 &&
      r.top >= 0 &&
      r.right <= innerWidth + 1 &&
      r.bottom <= innerHeight + 1 &&
      (hit === button || button.contains(hit))
    );
  });
  assert.equal(accessible, true, `unreachable UI: ${selector}`);
  await target.click({ timeout: 30000 });
}
export async function screenshot(page, name) {
  await page.screenshot({ path: `playwright-results/${name}.png` });
}
