import { Card, CardContent, Link, Typography, useTheme } from "@mui/material";
import Error from "../assets/Error.svg";

export const ErrorMessage = () => {
  const isLightMode = useTheme().palette.mode == "light";

  return <Card elevation={3}>
    <CardContent sx={{ textAlign: "center" }}>
      <img src={Error} height={150} style={ isLightMode ? {
      filter: "brightness(0) saturate(100%)"
    } : undefined}/>
      <Typography variant="h5" sx={{ my: "1rem" }}>
      An error occurred. Please try again later.
      </Typography>
      <Link 
        href="https://github.com/pkemkes/the-gist-of-it-sec/issues"
        underline="hover"
      >
        If the error persists, please reach out via GitHub.
      </Link>
    </CardContent>
  </Card>;
}