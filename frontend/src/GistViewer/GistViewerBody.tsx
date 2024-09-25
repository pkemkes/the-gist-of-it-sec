import { Box, Paper } from "@mui/material";
import { ReactNode } from "react";

type Props = {
  children: ReactNode,
  scrollRef?: React.RefObject<HTMLDivElement>
}

export const GistViewerBody = ({ children, scrollRef }: Props) => (
  <Paper
    sx = {{
      display: "flex",
      alignItems: "center",
      flexDirection: "column",
      position: "fixed",
      width: "100%",
      height: "100vh",
      overflow: "auto",
      overscrollBehaviorY: "contain",
    }}
    ref={ scrollRef }
  >
    <Box  
      sx={{
        width: "100%", 
        maxWidth: 1000,
        py: "5rem",
        px: "1rem",
      }}
    >
      { children }
    </Box>
  </Paper>
)