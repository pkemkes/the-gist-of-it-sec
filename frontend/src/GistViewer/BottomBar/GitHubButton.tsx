import { Button, useTheme } from "@mui/material";
import GitHubIcon from "@mui/icons-material/GitHub";


export const GitHubButton = () => {
  const isLightMode = useTheme().palette.mode == "light";

  const version = import.meta.env.VITE_APP_VERSION == undefined 
    ? "0.0.0" 
    : import.meta.env.VITE_APP_VERSION;
  
  return <Button
    component="a"
    href="https://github.com/pkemkes/the-gist-of-it-sec"
    target="_blank"
    sx={{ ml: "auto", mr: "1rem", textTransform: "none" }}
    endIcon={ <GitHubIcon /> }
    size="small"
    color={ isLightMode ? "secondary" : "primary" }
  >
    {`v${version}`}
  </Button>
};
