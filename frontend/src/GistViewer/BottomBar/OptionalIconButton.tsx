import { IconButton, Tooltip, useTheme } from "@mui/material";
import { ReactNode } from "react";

interface OptionalIconButtonProps {
  tooltip: string,
  urlEnvVar?: string,
  children: ReactNode,
}

export const OptionalIconButton = ({ tooltip, urlEnvVar, children }: OptionalIconButtonProps) => {
  const isLightMode = useTheme().palette.mode == "light";

  if (!urlEnvVar) {
    return undefined;
  }

  return <Tooltip title={ tooltip } placement="top-start">
    <IconButton
      component="a"
      href={ urlEnvVar }
      target="_blank"
      sx={{ ml: "0.5rem" }}
      size="small"
      color={ isLightMode ? "secondary" : "primary" }
    >
      { children }
    </IconButton>
  </Tooltip>
}