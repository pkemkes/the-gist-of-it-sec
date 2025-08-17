import { createApi, fetchBaseQuery } from "@reduxjs/toolkit/query/react";
import { Gist, FeedInfo, SimilarGist, SearchResult, Recap } from "./types";

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

const backendHostname = import.meta.env.VITE_BACKEND_URL == undefined 
  ? "http://localhost:8080" 
  : import.meta.env.VITE_BACKEND_URL;
const backendUrl = `${backendHostname}${backendHostname.endsWith("/") ? "" : "/"}api/v1/gists`;

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
        const params = [];
        if (lastGist != undefined) {
          params.push(`last_gist=${lastGist}`);
        }
        params.push(`take=${pageSize}`);
        params.push(`q=${encodeURIComponent(searchQuery)}`);
        params.push(`tags=${encodeURIComponent(tags.join(";;"))}`);
        params.push(`disabled_feeds=${JoinDisabledFeedsParam(disabledFeeds)}`);
        return `?${params.join("&")}`;
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
      query: ({ id }) => `${id}`,
    }),
    getSimilarGists: builder.query<SimilarGist[], SimilarGistsQueryParameters>({
      query: ({ id, disabledFeeds }) => `${id}/similar?disabled_feeds=${JoinDisabledFeedsParam(disabledFeeds)}`,
    }),
    getSearchResults: builder.query<SearchResult[], { id: number }>({
      query: ({ id }) => `${id}/searchResults`,
    }),
    getRecap: builder.query<Recap, { type: string }>({
      query: ({ type }) => `recap/${type}`,
    }),
  }),
});