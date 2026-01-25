import { combineReducers, configureStore } from "@reduxjs/toolkit";
import { backendApi } from "./backend";
import { TypedUseSelectorHook, useDispatch, useSelector } from "react-redux";
import { setupListeners } from "@reduxjs/toolkit/query";
import gistSlice, { initialState } from "./GistViewer/slice";
import throttle from "lodash.throttle";
import { loadDisabledFeeds, loadIncludeSponsoredContent, loadLanguageMode, loadTimezone, saveStateData } from "./localStorage";

const throttledSaveDisabledFeeds = throttle(() => {
  const state = store.getState().gists;
  saveStateData({
    disabledFeeds: state.disabledFeeds,
    timezone: state.timezone,
    languageMode: state.languageMode,
    includeSponsoredContent: state.includeSponsoredContent,
  })
}, 1000);

const rootReducer = combineReducers({
  [backendApi.reducerPath]: backendApi.reducer,
  gists: gistSlice,
});

export const store = configureStore({
  reducer: rootReducer,
  middleware: getDefaultMiddleware => 
    getDefaultMiddleware().concat(backendApi.middleware),
  preloadedState: { gists: { 
    ...initialState, 
    disabledFeeds: loadDisabledFeeds() ?? initialState.disabledFeeds,
    timezone: loadTimezone() ?? initialState.timezone,
    languageMode: loadLanguageMode() ?? initialState.languageMode,
    includeSponsoredContent: loadIncludeSponsoredContent() ?? initialState.includeSponsoredContent,
  }},
});

store.subscribe(throttledSaveDisabledFeeds);

export type AppDispatch = typeof store.dispatch;

setupListeners(store.dispatch);

export const useAppDispatch: () => AppDispatch = useDispatch;
export const useAppSelector: TypedUseSelectorHook<RootState> = useSelector;

export type RootState = ReturnType<typeof store.getState>;
