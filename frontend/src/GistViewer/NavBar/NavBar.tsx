import { AppBar, Toolbar } from "@mui/material"
import { ResetButton } from "./ResetButton"
import { FeedSelector } from "./FeedSelector"
import { useSearchParams } from "react-router"
import React from "react"
import { SearchBar } from "./SearchBar"
import { TitleIcon } from "./TitleIcon"

export const NavBar = () => {
  const [searchParams, _] = useSearchParams();
  const gistIdIsSet = searchParams.get("gist") != undefined;

  const tools = <React.Fragment>
    <SearchBar />
    <FeedSelector />
    <ResetButton />
  </React.Fragment> 

  return (
    <AppBar>
      <Toolbar disableGutters>
        <TitleIcon />
        { gistIdIsSet ? undefined : tools }
      </Toolbar>
    </AppBar>
  )
}