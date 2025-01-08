import { GistViewer } from "./GistViewer/GistViewer";
import { store } from "./store";
import { Provider } from "react-redux";
import { CssBaseline, ThemeProvider } from "@mui/material";
import { BrowserRouter, Route, Routes } from "react-router";
import { theme } from "./theme";


export const App = () => {
  return (
    <ThemeProvider theme={ theme }>
      <CssBaseline />
      <Provider store={ store }>
        <BrowserRouter>
          <Routes>
            <Route path="/" element={ <GistViewer /> } />
          </Routes>
        </BrowserRouter>
      </Provider>
    </ThemeProvider>
  );
}