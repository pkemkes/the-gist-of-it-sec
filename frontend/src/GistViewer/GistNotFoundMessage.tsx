import { Card, CardContent, Link, Typography, useTheme } from "@mui/material";
import HelpOutlineIcon from '@mui/icons-material/HelpOutline';

export const GistNotFoundMessage = () => {
  const isLightMode = useTheme().palette.mode == "light";

  return <Card elevation={3}>
    <CardContent sx={{ textAlign: "center" }}>
      {/* <img src={Error} height={150} style={ isLightMode ? {
      filter: "brightness(0) saturate(100%)"
    } : undefined}/> */}
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