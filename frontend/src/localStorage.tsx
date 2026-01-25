import { LanguageMode } from "./types";

const disabledFeedsLocalStorageKey = "disabledFeeds";
const timezoneStorageKey = "timezone";
const languageModeStorageKey = "languageMode";
const includeSponsoredContentStorageKey = "includeSponsoredContent";

interface SaveableStateData {
  disabledFeeds: number[],
  timezone: string,
  languageMode: LanguageMode,
  includeSponsoredContent: boolean,
}

export const saveStateData = (stateData: SaveableStateData) => {
	try {
    const serializedDisabledFeeds = JSON.stringify(stateData.disabledFeeds);
    localStorage.setItem(disabledFeedsLocalStorageKey, serializedDisabledFeeds);
    localStorage.setItem(timezoneStorageKey, stateData.timezone);
    localStorage.setItem(languageModeStorageKey, stateData.languageMode);
    localStorage.setItem(includeSponsoredContentStorageKey, JSON.stringify(stateData.includeSponsoredContent));
  } catch { }
};

export const loadDisabledFeeds = (): number[] | undefined => {
  try {
    const serializedDisabledFeeds = localStorage.getItem(disabledFeedsLocalStorageKey);
    if (serializedDisabledFeeds === null) return undefined;
    return JSON.parse(serializedDisabledFeeds);
  }
  catch {
    return undefined;
  }
};

export const loadTimezone = (): string | undefined => {
  const savedTimezone = localStorage.getItem(timezoneStorageKey);
  return savedTimezone === null ? undefined : savedTimezone;
};

export const loadLanguageMode = (): LanguageMode | undefined => {
  const savedLanguageMode = localStorage.getItem(languageModeStorageKey);
  if (savedLanguageMode == null) return undefined;
  if (Object.values(LanguageMode).includes(savedLanguageMode as LanguageMode)) {
    return savedLanguageMode as LanguageMode;
  }
  return undefined;
}

export const loadIncludeSponsoredContent = (): boolean | undefined => {
  const savedIncludeSponsoredContent = localStorage.getItem(includeSponsoredContentStorageKey);
  if (savedIncludeSponsoredContent == null) return undefined;
  try {
    return JSON.parse(savedIncludeSponsoredContent);
  } catch {
    return undefined;
  }
}