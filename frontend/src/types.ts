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
  gistId: number,
  title: string,
  snippet: string,
  url: string,
  displayUrl: string,
  thumbnailUrl: string | undefined,
  imageUrl: string | undefined
}

export interface RecapRelatedGist {
  id: number,
  title: string,
}

export interface RecapSection {
  heading: string,
  recap: string,
  related: RecapRelatedGist[],
}

export interface Recap {
  created: string,
  recapSections: RecapSection[],
  id: number,
}

export enum LanguageMode {
  ORIGINAL = "Original",
  ENGLISH = "En",
  GERMAN = "De",
}
