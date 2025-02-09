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

export interface FeedInfo {
  id: number,
  title: string,
  link: string,
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
