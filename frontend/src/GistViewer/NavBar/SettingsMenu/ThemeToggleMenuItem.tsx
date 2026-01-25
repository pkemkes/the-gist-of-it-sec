import { Box, ToggleButton, ToggleButtonGroup, Tooltip, useColorScheme } from "@mui/material";
import DarkModeOutlinedIcon from "@mui/icons-material/DarkModeOutlined";
import LightModeOutlinedIcon from "@mui/icons-material/LightModeOutlined";
import PhonelinkOutlinedIcon from "@mui/icons-material/PhonelinkOutlined";


export const ThemeToggleMenuItem = () => {
	const { mode, setMode } = useColorScheme();

  const handleModeChange = (
    _: React.MouseEvent<HTMLElement>,
    newMode: "light" | "dark" | "system" | null,
  ) => {
    setMode(newMode);
  }

  const iconSx = { fontSize: "1.5rem" };

  return <Box sx={{ px: "1rem", my: "0.5rem" }} >
    <ToggleButtonGroup
      value={ mode }
      exclusive
      onChange={ handleModeChange }
      aria-label="color mode"
      fullWidth
    >
      <Tooltip title="Change theme to 'dark'">
        <ToggleButton value="dark" aria-label="dark mode">
          <DarkModeOutlinedIcon sx={ iconSx } />
        </ToggleButton>
      </Tooltip>
      <Tooltip title="Change theme to 'light'">
        <ToggleButton value="light" aria-label="light mode">
          <LightModeOutlinedIcon sx={ iconSx } />
        </ToggleButton>
      </Tooltip>
      <Tooltip title="Change theme to 'system'">
        <ToggleButton value="system" aria-label="system mode">
          <PhonelinkOutlinedIcon sx={ iconSx } />
        </ToggleButton>
      </Tooltip>
    </ToggleButtonGroup>
  </Box>
}