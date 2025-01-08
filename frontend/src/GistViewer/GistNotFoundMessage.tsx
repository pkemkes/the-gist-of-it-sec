import { Card, CardContent, Typography } from "@mui/material";
import HelpOutlineIcon from "@mui/icons-material/HelpOutline";

export const GistNotFoundMessage = () => {
  return <Card elevation={ 3 }>
    <CardContent sx={{ textAlign: "center" }}>
	    <HelpOutlineIcon sx={{ fontSize: "10rem" }} />
      <Typography variant="h5" sx={{ my: "1rem" }}>
        This gist could not be found. Maybe it was removed from the feed?
      </Typography>
      <Typography variant="body1">
        Sorry ☹️
      </Typography>
    </CardContent>
  </Card>;
}