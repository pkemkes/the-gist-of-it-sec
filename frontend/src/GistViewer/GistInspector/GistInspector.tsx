import { ToggleButtonGroup, ToggleButton } from "@mui/material";
import { backendApi } from "../../services/backend";
import { GistViewerBody } from "../GistViewerBody";
import { GistCard } from "../GistCard";
import { BackButton } from "./BackButton";
import React from "react";
import { SimilarGistList } from "./SimilarGistList";
import { SearchResultList } from "./SearchResultList";
import { useNavigate, useSearchParams } from "react-router";
import { LoadingBar } from "../LoadingBar";
import { ErrorMessage } from "../ErrorMessage";

interface GistInspectorProps {
  gistId: number
}

export const GistInspector = ({ gistId }: GistInspectorProps) => {
  const [searchParams, _] = useSearchParams();
  let mode = searchParams.get("mode") ?? "similar";

  const navigate = useNavigate();

  const handleModeChange = (
    _: React.MouseEvent<HTMLElement>,
    newMode: string,
  ) => {
    navigate(`/?gist=${gistId}&mode=${newMode}`);
  };

  const { data, error, isFetching } = backendApi.useGetGistByIdQuery({ id: gistId });

  if (isFetching) {
    return <GistViewerBody>
      <LoadingBar />
    </GistViewerBody>
  }

  if (error || data == undefined) {
    return <GistViewerBody>
      <ErrorMessage />
    </GistViewerBody>
  }

  return <GistViewerBody>
    <BackButton />
    <GistCard gist={data} highlighted />
    <ToggleButtonGroup
      color="primary"
      value={mode}
      exclusive
      onChange={handleModeChange}
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
        : <SearchResultList gistId={ gistId } searchQuery={ data.search_query } /> 
    }
  </GistViewerBody>
}