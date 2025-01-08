import { AppBar, Toolbar } from "@mui/material"
import { ResetButton } from "./ResetButton"
import { useSearchParams } from "react-router"
import React from "react"
import { SearchBar } from "./SearchBar"
import { TitleIcon } from "./TitleIcon"
import { SettingsMenu } from "./SettingsMenu/SettingsMenu"

export const NavBar = () => {
  const [searchParams, _] = useSearchParams();
  const gistIdIsSet = searchParams.get("gist") != undefined;

  const tools = <React.Fragment>
    <SearchBar />
    <ResetButton />
  </React.Fragment> 

  return (
    <AppBar>
      <Toolbar disableGutters>
        <TitleIcon />
        { gistIdIsSet ? undefined : tools }
        <SettingsMenu />
      </Toolbar>
    </AppBar>
  )
}