import { combineReducers, configureStore } from '@reduxjs/toolkit';
import { backendApi } from "./services/backend";
import { TypedUseSelectorHook, useDispatch, useSelector } from 'react-redux';
import { setupListeners } from '@reduxjs/toolkit/query';
import gistSlice, { initialState } from './GistViewer/slice';
import throttle from 'lodash.throttle';
import { loadDisabledFeeds, saveDisabledFeeds } from './localStorage';

const throttledSaveDisabledFeeds = throttle(() => 
  saveDisabledFeeds(store.getState().gists.disabledFeeds), 1000);

const rootReducer = combineReducers({
  [backendApi.reducerPath]: backendApi.reducer,
  gists: gistSlice,
});

export const store = configureStore({
  reducer: rootReducer,
  middleware: getDefaultMiddleware => 
    getDefaultMiddleware().concat(backendApi.middleware),
  preloadedState: { gists: { ...initialState, disabledFeeds: loadDisabledFeeds() ?? initialState.disabledFeeds } }
});

store.subscribe(throttledSaveDisabledFeeds);

export type AppDispatch = typeof store.dispatch

setupListeners(store.dispatch);

export const useAppDispatch: () => AppDispatch = useDispatch;
export const useAppSelector: TypedUseSelectorHook<RootState> = useSelector;

export type RootState = ReturnType<typeof store.getState>
