import { AppBar, Toolbar } from "@mui/material"
import { GitHubButton } from "./GitHubButton"
import { ThemeToggle } from "./ThemeToggle"
import { TelegramButton } from "./TelegramButton"
import { OptionalTextButton } from "./OptionalTextButton"

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
      <ThemeToggle />
      <TelegramButton />
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