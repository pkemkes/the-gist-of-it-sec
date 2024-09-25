import { IconButton, Tooltip, useColorScheme } from "@mui/material"
import DarkModeOutlinedIcon from '@mui/icons-material/DarkModeOutlined';
import LightModeOutlinedIcon from '@mui/icons-material/LightModeOutlined';
import PhonelinkOutlinedIcon from '@mui/icons-material/PhonelinkOutlined';


export const ThemeToggle = () => {
  const { mode, setMode } = useColorScheme();

  const toggleTheme = () => {
    switch (mode) {
      case "system":
        setMode("dark");
        break;
      case "dark":
        setMode("light");
        break;
      case "light":
        setMode("system");
        break;
    }
  }
  
  if (!mode) {
    return null;
  }

  return <div>
    <Tooltip title={ `Change theme. Currently: ${mode}` } placement="top-start">
      <IconButton 
        onClick={toggleTheme}
        size='small'
        edge='start'
        color='inherit'
        sx={{
          ml: '1rem'
        }}
      >
        { 
          mode == "system" 
            ? <PhonelinkOutlinedIcon sx={{ fontSize: "1rem" }} />
            : mode == "light" 
              ? <LightModeOutlinedIcon sx={{ fontSize: "1rem" }} /> 
              : <DarkModeOutlinedIcon sx={{ fontSize: "1rem" }} />
        }
      </IconButton>
    </Tooltip>
  </div>
}