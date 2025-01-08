export const OpenLinkInNewTab = (link: string) => {
  const newTab = window.open(link, "_blank");
  if (newTab != null) {
    newTab.focus();
  }
};