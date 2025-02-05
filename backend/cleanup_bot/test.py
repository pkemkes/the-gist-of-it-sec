def _gist_should_be_disabled(self, link: str) -> bool:
	if any(link.startswith(domain) for domain in self._domains_to_ignore):
		return False
	session = self._get_session()
	resp = session.head(link, headers=self._headers)
	if resp.status_code == 400:
		return True
	if resp.is_redirect:
		if link not in self._get_entry_links_for_feed(self._feeds[gist.feed_id]):
			return True
	return False