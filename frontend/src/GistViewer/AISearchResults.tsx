import { backendApi } from "../backend";
import { GistCard } from "./GistCard";
import { GistEndCard } from "./GistEndCard";
import { GistViewerBody } from "./GistViewerBody";
import { LoadingBar } from "./LoadingBar";
import { ErrorMessage } from "./ErrorMessage";
import { useAppDispatch, useAppSelector } from "../store";
import { aiSearchQueryChanged, selectAiSearchQuery, selectDisabledFeeds, selectLanguageMode } from "./slice";
import { Button, Typography } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";

export const AISearchResults = () => {
  const dispatch = useAppDispatch();
  const aiSearchQuery = useAppSelector(selectAiSearchQuery)!;
  const disabledFeeds = useAppSelector(selectDisabledFeeds);
  const languageMode = useAppSelector(selectLanguageMode);
  const { data, error, isFetching } = backendApi.useSearchGistsQuery({ query: aiSearchQuery, disabledFeeds, languageMode });

  const handleBack = () => dispatch(aiSearchQueryChanged(undefined));

  if (error || data == undefined && !isFetching) {
    return <GistViewerBody>
      <Button onClick={ handleBack } variant="outlined" startIcon={<ArrowBackIcon />} sx={{ mr: "auto", mb: "20px", width: "8rem" }}>Back</Button>
      <ErrorMessage />
    </GistViewerBody>
  }

  const dataToDisplay = data ? data.map(({ gist, similarity }, i) => (
    <GistCard key={ i } gist={ gist } similarity={ similarity } />
  )) : [];

  dataToDisplay.push(isFetching
    ? <LoadingBar key={dataToDisplay.length} />
    : <GistEndCard key={dataToDisplay.length} />
  );

  return <GistViewerBody>
    <Button onClick={ handleBack } variant="outlined" startIcon={<ArrowBackIcon />} sx={{ mr: "auto", mb: "20px", width: "8rem" }}>Back</Button>
    <Typography variant="h4" sx={{ ml: "1rem", mt: "1rem", mb: "1rem" }}>
      AI search results for &quot;{aiSearchQuery}&quot;:
    </Typography>
    { dataToDisplay }
  </GistViewerBody>
}
