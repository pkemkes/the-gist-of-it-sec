import { Box, Button } from "@mui/material";
import { backendApi } from "../../services/backend";
import { SearchResultCard } from "./SearchResultCard";
import GoogleIcon from '@mui/icons-material/Google';
import { LoadingBar } from "../LoadingBar";
import { GistEndCard } from "../GistEndCard";
import { ErrorMessage } from "../ErrorMessage";

interface SearchResultListProps {
  gistId: number,
  searchQuery: string,
}

const dummyEmptyString = "DUMMYEMPTYSEARCHRESULT";

export const SearchResultList = ({ gistId, searchQuery }: SearchResultListProps) => {
  const { data, error, isFetching } = backendApi.useGetSearchResultsQuery({ id: gistId });

  if (error || data == undefined && !isFetching) {
    return <ErrorMessage />;
  }

  const searchQueryHeader = <Box key={-1}>
    <Button
      component="a"
      href={ "https://www.google.com/search?q=" + encodeURIComponent(searchQuery) }
      target="_blank"
      size="large"
      sx={{ mb: "1rem" }}
    >
      <GoogleIcon sx={{ mr: "1rem" }} />
      { searchQuery }
    </Button>
  </Box>;

  let dataToDisplay = [ searchQueryHeader ];

  const filteredData = data ? data.filter(sr => (
    sr.title != dummyEmptyString 
    && sr.snippet != dummyEmptyString
    && sr.link != dummyEmptyString
    && sr.display_link != dummyEmptyString
  )) : [];

  dataToDisplay = dataToDisplay.concat(filteredData.map((searchResult, i) => (
    <SearchResultCard key={ i } searchResult={ searchResult } />
  )));

  dataToDisplay.push(isFetching 
    ? <LoadingBar key={dataToDisplay.length} /> 
    : <GistEndCard key={dataToDisplay.length} />
  );

  return dataToDisplay;
};
