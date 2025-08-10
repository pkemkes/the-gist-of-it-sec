export interface Gist {
  id: number,
  feedTitle: string,
  feedUrl: string,
  title: string,
  author: string,
  url: string,
  published: string,
  updated: string,
  summary: string,
  tags: string[],
  searchQuery: string,
}

export interface FeedInfo {
  id: number,
  title: string,
  rssUrl: string,
  language: string,
}

export interface SimilarGist {
  gist: Gist,
  similarity: number,
}

export interface SearchResult {
  id: number,
  gist_id: number,
  title: string,
  snippet: string,
  link: string,
  display_link: string,
  thumbnail_link: string | undefined,
  image_link: string | undefined
}

export interface RecapRelatedGist {
  id: number,
  title: string,
}

export interface RecapCategory {
  heading: string,
  recap: string,
  related: RecapRelatedGist[],
}

export interface Recap {
  created: string,
  recap: RecapCategory[],
}
