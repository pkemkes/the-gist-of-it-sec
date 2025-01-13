import { createApi, fetchBaseQuery } from "@reduxjs/toolkit/query/react";
import { Gist, FeedInfo, SimilarGist, SearchResult } from "./types";

interface GistsQueryParameters {
  lastGist: number | undefined,
  searchQuery: string,
  tags: string[],
  disabledFeeds: number[],
}

interface SimilarGistsQueryParameters {
  id: number, 
  disabledFeeds: number[],
}

const pageSize = 20;

const backendUrl = import.meta.env.VITE_BACKEND_URL == undefined 
  ? "http://localhost:8080/" 
  : import.meta.env.VITE_BACKEND_URL;

const JoinDisabledFeedsParam = (disabledFeeds: number[]) => 
  encodeURIComponent(disabledFeeds.join(","));

export const backendApi = createApi({
  reducerPath: "backendApi",
  baseQuery: fetchBaseQuery({
    baseUrl: backendUrl,
  }),
  endpoints: builder => ({
    getGists: builder.query<Gist[], GistsQueryParameters>({
      query: ({ lastGist, searchQuery, tags, disabledFeeds }) => {
        let pathAndParams = "gists?";
        if (lastGist != undefined) {
          pathAndParams += `last_gist=${lastGist}&`
        }
        pathAndParams += `take=${pageSize}&`;
        pathAndParams += `q=${encodeURIComponent(searchQuery)}&`
        pathAndParams += `tags=${encodeURIComponent(tags.join(";;"))}&`;
        pathAndParams += `disabled_feeds=${JoinDisabledFeedsParam(disabledFeeds)}`;
        return pathAndParams;
      },
      // taken from: https://redux-toolkit.js.org/rtk-query/api/createApi#merge
      serializeQueryArgs: ({ queryArgs }) => {
        const { lastGist, ...newQueryArgs } = queryArgs;
        return newQueryArgs
      },
      // Always merge incoming data to the cache entry
      merge: (currentCache, newItems) => {
        const ids = new Set(currentCache.map(gist => gist.id));
        newItems.forEach(gist => {
          if (!ids.has(gist.id)) {
            currentCache.push(gist);
          }
        });
      },
      // Refetch when the page arg changes
      forceRefetch({ currentArg, previousArg }) {
        return (
          currentArg?.lastGist !== previousArg?.lastGist 
          || currentArg?.searchQuery !== previousArg?.searchQuery
        )
      },
    }),
    getAllFeedInfo: builder.query<FeedInfo[], void>({
      query: () => "feeds",
    }),
    getGistById: builder.query<Gist, { id: number }>({
      query: ({ id }) => `gists/by_id?id=${id}`,
    }),
    getSimilarGists: builder.query<SimilarGist[], SimilarGistsQueryParameters>({
      query: ({ id, disabledFeeds }) => {
        let pathAndParams = "gists/similar?";
        pathAndParams += `id=${id}&`;
        pathAndParams += `disabled_feeds=${JoinDisabledFeedsParam(disabledFeeds)}`;
        return pathAndParams
      },
    }),
    getSearchResults: builder.query<SearchResult[], { id: number }>({
      query: ({ id }) => `gists/search_results?id=${id}`,
    }),
  }),
});