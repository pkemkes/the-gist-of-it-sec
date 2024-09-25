import { createSlice, PayloadAction } from "@reduxjs/toolkit";
import { type RootState } from '../store';

export const name = 'gists'

export interface GistsState {
  lastGist: number | undefined,
  searchQuery: string,
  tags: string[],
  disabledFeeds: number[],
}

export const initialState: GistsState = {
  lastGist: undefined,
  searchQuery: "",
  tags: [],
  disabledFeeds: [],
}

export const slice = createSlice({
  name: name,
  initialState: initialState,
  reducers: {
    lastGistChanged: (state, action: PayloadAction<number | undefined>) => {
      state.lastGist = action.payload;
    },
    lastGistReset: (state) => {
      state.lastGist = initialState.lastGist;
    },
    searchQueryChanged: (state, action: PayloadAction<string>) => {
      state.searchQuery = action.payload;
      state.lastGist = initialState.lastGist;
    },
    tagToggled: (state, action: PayloadAction<string>) => {
      if (state.tags.includes(action.payload)){
        state.tags.splice(state.tags.indexOf(action.payload), 1)
      } else {
        state.tags.push(action.payload)
      }
      state.lastGist = initialState.lastGist;
    },
    disabledFeedToggled: (state, action: PayloadAction<number>) => {
      if (state.disabledFeeds.includes(action.payload)){
        state.disabledFeeds.splice(state.disabledFeeds.indexOf(action.payload), 1)
      } else {
        state.disabledFeeds.push(action.payload)
      }
    },
    gistListReset: (state) => {
      state.lastGist = initialState.lastGist;
      state.tags = initialState.tags;
    },
  }
});

export const {
  lastGistChanged,
  lastGistReset,
  searchQueryChanged,
  tagToggled,
  disabledFeedToggled,
  gistListReset,
} = slice.actions;

export const selectLastGist = (state: RootState) => state.gists.lastGist;
export const selectSearchQuery = (state: RootState) => state.gists.searchQuery;
export const selectTags = (state: RootState) => state.gists.tags;
export const selectDisabledFeeds = (state: RootState) => state.gists.disabledFeeds;

export default slice.reducer;
