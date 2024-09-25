import { GistViewer } from "./GistViewer/GistViewer";
import { store } from "./store";
import { Provider } from "react-redux";
import { createTheme, CssBaseline, ThemeProvider } from "@mui/material";
import { BrowserRouter, Route, Router, Routes } from "react-router";
import { lightBlue, orange } from "@mui/material/colors";

const theme = createTheme({
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

export const App = () => {
  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <Provider store={store}>
        <BrowserRouter>
          <Routes>
            <Route path="/" element={<GistViewer />} />
          </Routes>
        </BrowserRouter>
      </Provider>
    </ThemeProvider>
  );
}