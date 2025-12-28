import { ToggleButtonGroup, ToggleButton } from "@mui/material";
import { backendApi } from "../../backend";
import { GistViewerBody } from "../GistViewerBody";
import { GistCard } from "../GistCard";
import { BackButton } from "../BackButton";
import React, { useState } from "react";
import { SimilarGistList } from "./SimilarGistList";
import { SearchResultList } from "./SearchResultList";
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
  let [ mode, setMode ] = useState("similar");
  const languageMode = useAppSelector(selectLanguageMode);

  const handleModeChange = (
    _: React.MouseEvent<HTMLElement>,
    newMode: string,
  ) => {
    setMode(newMode);
  };

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
    <ToggleButtonGroup
      color="primary"
      value={ mode }
      exclusive
      onChange={ handleModeChange }
      sx={{
        display: "grid",
        gridTemplateColumns: "50% auto",
        mb: "1rem",
      }}
    >
      <ToggleButton value="similar" size="small">Similar Gists</ToggleButton>
      <ToggleButton value="search" size="small">Search Results</ToggleButton>
    </ToggleButtonGroup>
    { 
      mode == "similar" 
        ? <SimilarGistList gistId={ gistId } />
        : <SearchResultList gistId={ gistId } searchQuery={ data.searchQuery } /> 
    }
  </GistViewerBody>
}