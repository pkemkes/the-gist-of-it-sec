import { LanguageMode } from "./types";

const disabledFeedsLocalStorageKey = "disabledFeeds";
const timezoneStorageKey = "timezone";
const languageModeStorageKey = "languageMode";

interface SaveableStateData {
  disabledFeeds: number[],
  timezone: string,
  languageMode: LanguageMode,
}

export const saveStateData = (stateData: SaveableStateData) => {
	try {
    const serializedDisabledFeeds = JSON.stringify(stateData.disabledFeeds);
    localStorage.setItem(disabledFeedsLocalStorageKey, serializedDisabledFeeds);
    localStorage.setItem(timezoneStorageKey, stateData.timezone);
    localStorage.setItem(languageModeStorageKey, stateData.languageMode);
  }
  catch { }
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