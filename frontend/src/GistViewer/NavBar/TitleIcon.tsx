import { Box, useTheme } from "@mui/material"
import Logo from "../../assets/Logo.svg"


export const TitleIcon = () => {
  const isLightMode = useTheme().palette.mode == "light";

  return <Box
    component="a"
    href="/"
    sx={{ 
      flexGrow: 1, 
      display: "flex", 
      justifyContent: "center", 
      flexDirection: "column",
      mx: "1rem",
  }}>
    <img src={Logo} width={90} style={ isLightMode ? {
      filter: "brightness(0) saturate(100%)"
    } : undefined} />
  </Box>
};