const disabledFeedsLocalStorageKey = "disabledFeeds";
const timezoneStorageKey = "timezone"

interface SaveableStateData {
  disabledFeeds: number[],
  timezone: string,
}

export const saveStateData = (stateData: SaveableStateData) => {
	try {
    const serializedDisabledFeeds = JSON.stringify(stateData.disabledFeeds);
    localStorage.setItem(disabledFeedsLocalStorageKey, serializedDisabledFeeds);
    localStorage.setItem(timezoneStorageKey, stateData.timezone);
  }
  catch { }
};

export const loadDisabledFeeds = (): number[] | undefined => {
  try {
    const serializedDisabledFeeds = localStorage.getItem(disabledFeedsLocalStorageKey);
    if (serializedDisabledFeeds === null) return undefined
    return JSON.parse(serializedDisabledFeeds)
  }
  catch {
    return undefined;
  }
};

export const loadTimezone = (): string | undefined => {
  const savedTimezone = localStorage.getItem(timezoneStorageKey);
  return savedTimezone === null ? undefined : savedTimezone;
};