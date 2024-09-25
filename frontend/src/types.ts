export type GistsBackendResponse = Gist[];

export interface Gist {
  id: number,
  feed_title: string,
  feed_link: string,
  title: string,
  author: string,
  link: string,
  published: string,
  updated: string,
  summary: string,
  tags: string[],
  search_query: string,
}

export type FeedInfoBackendResponse = FeedInfo[];

export interface FeedInfo {
  id: number,
  title: string,
  language: string,
}

export type SimilarGistsBackendResponse = SimilarGist[];

export interface SimilarGist {
  gist: Gist,
  similarity: number,
}

export type SearchResultsBackendResponse = SearchResult[];

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