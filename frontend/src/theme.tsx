import { createTheme } from "@mui/material";
import { lightBlue, orange } from "@mui/material/colors";

export const theme = createTheme({
	colorSchemes: {
	  dark: {
      palette: {
        primary: orange,
        secondary: {
          main: "#FFFFFF",
        }
      }
	  },
	  light: {
      palette: {
        primary: lightBlue,
        secondary: {
          main: "#000000",
        }
      }
	  }
	}
});