import { Box, Typography } from "@mui/material";

export const GistEndCard = () => (
  <Box sx={{ 
    textAlign: "center",
    width: "95%",
    maxWidth: 1000,
    mt: "20px",
    flexGrow: 1,
  }}>
    <Typography sx={{ color: "text.secondary", fontSize: 16, my: "20px" }}>
      This is the end.
    </Typography>
  </Box>
)