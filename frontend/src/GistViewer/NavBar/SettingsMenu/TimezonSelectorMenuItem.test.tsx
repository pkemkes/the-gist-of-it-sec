import { act } from "react";
import { createRoot } from "react-dom/client";
import { Provider } from "react-redux";
import { MenuList } from "@mui/material";
import { expect, it, vi } from "vitest";
import { store } from "../../../store";
import { timezoneChanged } from "../../slice";
import { TimezoneSelectorMenuItem } from "./TimezonSelectorMenuItem";

it("displays the saved timezone and keeps named zones with the same offset", async () => {
  vi.stubGlobal("IS_REACT_ACT_ENVIRONMENT", true);
  const container = document.createElement("div");
  document.body.append(container);
  const root = createRoot(container);
  try {
    await act(async () => {
      store.dispatch(timezoneChanged("Europe/Berlin"));
      root.render(<Provider store={store}><MenuList><TimezoneSelectorMenuItem /></MenuList></Provider>);
    });
    const select = container.querySelector("#timezone-select")!;
    expect(select.textContent).toContain("Berlin");
    await act(async () => {
      select.dispatchEvent(new KeyboardEvent("keydown", { bubbles: true, key: "ArrowDown" }));
    });
    expect(document.querySelector('[role="option"][data-value="Europe/Amsterdam"]')).not.toBeNull();
    expect(document.querySelector('[role="option"][data-value="Asia/Manila"]')).not.toBeNull();
  } finally {
    await act(async () => root.unmount());
    container.remove();
    vi.unstubAllGlobals();
  }
});
