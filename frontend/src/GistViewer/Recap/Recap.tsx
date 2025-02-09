import { Box, Card, CardContent, List, Typography } from "@mui/material"
import { useSearchParams } from "react-router";
import { GistViewerBody } from "../GistViewerBody";
import { BackButton } from "../BackButton";
import { backendApi } from "../../backend";
import { LoadingBar } from "../LoadingBar";
import { ErrorMessage } from "../ErrorMessage";
import { RelatedGist } from "./RelatedGist";
import { ToLocaleString } from "../utils";
import { useAppSelector } from "../../store";
import { selectTimezone } from "../slice";

export const Recap = () => {
	const [searchParams, _] = useSearchParams();
  const timezone = useAppSelector(selectTimezone);
	const recapType = searchParams.get("recap") == "daily" ? "daily" : "weekly";

  const { data, error, isFetching } = backendApi.useGetRecapQuery({ type: recapType });

  if (error || data == undefined && !isFetching) {
    return <GistViewerBody>
		  <BackButton />
      <ErrorMessage />
    </GistViewerBody>
  }

  const recapCard = isFetching 
    ? undefined 
    : <Box>
        <Typography variant="h4" sx={{ ml: "1rem" }}>
          News of { recapType == "daily" ? "yesterday" : "last 7 days" }
        </Typography>
        <Typography variant="subtitle1" sx={{ ml: "1rem", mb: "1rem" }}>
          Created: { ToLocaleString(data!.created, timezone) }
        </Typography>
        { 
          data?.recap.map(( category, i ) =>
            <Card elevation={ 3 } sx={{ mb: "1rem" }} key={ i }> 
              <CardContent>
                <Typography variant="h5" sx={{ mb: "0.5rem" }}>
                  { category.heading }
                </Typography>
                <Typography variant="body2" sx={{ mb: "0.5rem" }}>
                  { category.recap }
                </Typography>
                <Typography variant="subtitle1">
                  Related gists:
                </Typography>
                <List sx={{ p: 0 }}>
                  { category.related.map(
                    ({ id, title }, i) => <RelatedGist id={ id } title={ title } key={ i } />
                  ) }
                </List>
              </CardContent>
            </Card>
          )
        }
      </Box>;

	return <GistViewerBody>
		<BackButton />
		{ recapCard }
    { isFetching ? <LoadingBar /> : undefined }
	</GistViewerBody>
}