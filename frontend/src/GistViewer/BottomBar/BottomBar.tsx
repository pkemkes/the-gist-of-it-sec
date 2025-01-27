import { AppBar, Toolbar } from "@mui/material"
import { GitHubButton } from "./GitHubButton"
import { OptionalTextButton } from "./OptionalTextButton"
import { OptionalIconButton } from "./OptionalIconButton"
import MultilineChartIcon from '@mui/icons-material/MultilineChart';
import TelegramIcon from "@mui/icons-material/Telegram";

export const BottomBar = () => {
	return <AppBar position="fixed" sx={{ 
      top: "auto", 
      bottom: 0,
      h: "1rem", 
    }}>
    <Toolbar 
      variant="dense"
      disableGutters
      sx={{ 
        h: "2.5rem", 
        minHeight: "2.5rem",
      }}
    >
      <OptionalIconButton urlEnvVar={ import.meta.env.VITE_METRICS_URL }>
        <MultilineChartIcon />
      </OptionalIconButton>
      <OptionalIconButton urlEnvVar={ import.meta.env.VITE_TELEGRAM_URL }>
        <TelegramIcon />
      </OptionalIconButton>
      <OptionalTextButton
        urlEnvVar={ import.meta.env.VITE_IMPRINT_URL } 
        label="Imprint" 
      />
      <OptionalTextButton 
        urlEnvVar={ import.meta.env.VITE_PRIVACY_URL }
        label="Privacy Policy"
      />
      <GitHubButton />
    </Toolbar>
  </AppBar>
}