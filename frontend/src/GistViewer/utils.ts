export const OpenLinkInNewTab = (link: string) => {
  const newTab = window.open(link, "_blank");
  if (newTab != null) {
    newTab.focus();
  }
};

export const ToLocaleString = (isoTime: string, timezone: string) => (
  new Date(isoTime).toLocaleString("de-DE", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    timeZone: timezone,
  })
);