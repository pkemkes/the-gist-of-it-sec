import SettingsIcon from "@mui/icons-material/Settings";
import { IconButton, Menu } from "@mui/material";
import React from "react";
import { FeedSelectorMenuItem } from "./FeedSelectorMenuItem";
import { ThemeToggleMenuItem } from "./ThemeToggleMenuItem";
import { TimezoneSelectorMenuItem } from "./TimezonSelectorMenuItem";
import { useSearchParams } from "react-router";


export const SettingsMenu = () => {
  const [anchorEl, setAnchorEl] = React.useState<null | HTMLElement>(null);
  const menuOpened = Boolean(anchorEl);
  const [searchParams, _] = useSearchParams();
  const isInInspectorMode = searchParams.get("gist") != undefined;

  const handleMenuClick = (event: React.MouseEvent<HTMLElement>) => {
    setAnchorEl(event.currentTarget);
  };

  const handleMenuClose = () => {
    setAnchorEl(null)
  };

	return <div>
    <IconButton
      id="menu-button"
      aria-controls={ menuOpened ? "settings-menu" : undefined }
      aria-expanded={ menuOpened ? "true" : undefined }
      aria-haspopup="true"
      onClick={ handleMenuClick }
      sx={{ mr: "1rem" }}
    >
      <SettingsIcon />
    </IconButton>
    <Menu
      id="settings-menu"
      aria-labelledby="menu-button"
      anchorEl={ anchorEl }
      open={ menuOpened }
      onClose={ handleMenuClose }
      anchorOrigin={{
        vertical: "bottom",
        horizontal: "right",
      }}
      transformOrigin={{
        vertical: "top",
        horizontal: "right",
      }}
    >
      { isInInspectorMode ? undefined : <FeedSelectorMenuItem /> }
      <TimezoneSelectorMenuItem />
      <ThemeToggleMenuItem />
    </Menu>
  </div>
}