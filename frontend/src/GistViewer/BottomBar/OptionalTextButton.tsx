import { Button, useTheme } from "@mui/material";


interface OptionalTextButtonProps {
	urlEnvVar?: string,
  label: string
}

export const OptionalTextButton = ({ urlEnvVar, label }: OptionalTextButtonProps) => {
  const isLightMode = useTheme().palette.mode == "light";

  if (!urlEnvVar) {
    return undefined;
  }

  return <Button
    component="a"
    href={ urlEnvVar }
    target="_blank"
    sx={{ ml: "0.3rem" }}
    color={ isLightMode ? "secondary" : "primary" }
  >
    { label }
  </Button>
}