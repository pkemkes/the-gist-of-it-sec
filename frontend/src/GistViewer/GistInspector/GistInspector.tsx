import { Typography } from "@mui/material";
import { backendApi } from "../../backend";
import { GistViewerBody } from "../GistViewerBody";
import { GistCard } from "../GistCard";
import { BackButton } from "../BackButton";
import { SimilarGistList } from "./SimilarGistList";
import { LoadingBar } from "../LoadingBar";
import { ErrorMessage } from "../ErrorMessage";
import { FetchBaseQueryError } from "@reduxjs/toolkit/query";
import { GistNotFoundMessage } from "../GistNotFoundMessage";
import { useAppSelector } from "../../store";
import { selectLanguageMode } from "../slice";

interface GistInspectorProps {
  gistId: number
}

export const GistInspector = ({ gistId }: GistInspectorProps) => {
  const languageMode = useAppSelector(selectLanguageMode);

  const { data, error, isFetching } = backendApi.useGetGistByIdQuery({ id: gistId, languageMode });

  if (isFetching) {
    return <GistViewerBody>
      <LoadingBar />
    </GistViewerBody>
  }

  const isParsingError = (error: any): error is FetchBaseQueryError => {
    return (
      typeof error === "object" &&
      error !== null &&
      error.status === "PARSING_ERROR" &&
      typeof error.originalStatus === "number" &&
      typeof error.data === "string" &&
      typeof error.error === "string"
    )
  };

  if (error != undefined && isParsingError(error) && error.status == "PARSING_ERROR" && error.originalStatus == 404 ) {
    return <GistViewerBody>
      <BackButton />
      <GistNotFoundMessage />
    </GistViewerBody>
  }

  if (error || data == undefined) {
    return <GistViewerBody>
      <BackButton />
      <ErrorMessage />
    </GistViewerBody>
  }

  return <GistViewerBody>
    <BackButton />
    <GistCard gist={ data } highlighted />
    <Typography variant="h4" sx={{ ml: "1rem", mt: "2rem", mb: "1rem" }}>
      Similar gists:
    </Typography>
    <SimilarGistList gistId={ gistId } />
  </GistViewerBody>
}