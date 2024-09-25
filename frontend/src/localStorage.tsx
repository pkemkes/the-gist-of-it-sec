import { RootState } from "./store";

const disabledFeedsLocalStorageKey = "disabledFeeds";

export const saveDisabledFeeds = (disabledFeeds: number[]) => {
	try {
    const serializedDisabledFeeds = JSON.stringify(disabledFeeds);
    localStorage.setItem(disabledFeedsLocalStorageKey, serializedDisabledFeeds);
  }
  catch { }
};

export const loadDisabledFeeds = () => {
  try {
    const serializedDisabledFeeds = localStorage.getItem(disabledFeedsLocalStorageKey);
    if (serializedDisabledFeeds === null) return undefined
    return JSON.parse(serializedDisabledFeeds)
  }
  catch {
    return undefined;
  }
};