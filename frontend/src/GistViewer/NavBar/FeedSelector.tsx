import { Box, IconButton, Menu, ToggleButton, ToggleButtonGroup, Tooltip } from "@mui/material";
import FilterListIcon from '@mui/icons-material/FilterList';
import React from "react";
import { FeedInfo } from "../../types";
import { backendApi } from "../../services/backend";
import { useAppDispatch, useAppSelector } from "../../store";
import { disabledFeedToggled, lastGistReset, selectDisabledFeeds } from "../slice"


export const FeedSelector = () => {
  const dispatch = useAppDispatch();
  const disabledFeeds = useAppSelector(selectDisabledFeeds);

  const [anchorEl, setAnchorEl] = React.useState<null | HTMLElement>(null);
  const menuIsOpened = Boolean(anchorEl);
  const handleClick = (event: React.MouseEvent<HTMLButtonElement>) => {
    setAnchorEl(event.currentTarget);
  };
  const handleClose = () => {
    setAnchorEl(null);
  };
  
  const { data, error, isFetching } = backendApi.useGetAllFeedInfoQuery();

  const allFeedInfoToDisplay = isFetching || !data || error ? [] : data;

  const FeedIsEnabled = (feedId: number) => !disabledFeeds.includes(feedId);

  return (
    <Box sx={{ mr: "0.7rem" }}>
      <Tooltip title="Select Feeds" placement="bottom">
        <IconButton
          aria-controls={menuIsOpened ? 'feed-selector-menu' : undefined}
          aria-haspopup="true"
          aria-expanded={menuIsOpened ? 'true' : undefined}
          onClick={handleClick}
        >
          <FilterListIcon />
        </IconButton>
      </Tooltip>
      <Menu
        id="feed-selector-menu"
        anchorEl={anchorEl}
        open={menuIsOpened}
        onClose={handleClose}
        MenuListProps={{
          'aria-labelledby': 'feed-selector-button',
        }}
        anchorOrigin={{
          vertical: 'bottom',
          horizontal: 'right',
        }}
        transformOrigin={{
          vertical: 'top',
          horizontal: 'right',
        }}
      >
        <ToggleButtonGroup
          orientation="vertical"
        >
          { allFeedInfoToDisplay.map((feedInfo: FeedInfo, index: number) => (
            <ToggleButton
              value={feedInfo.id}
              aria-label={feedInfo.title}
              key={index}
              selected={FeedIsEnabled(feedInfo.id)}
              onClick={ () => {
                dispatch(disabledFeedToggled(feedInfo.id));
                dispatch(lastGistReset());
              }}
            >
              {feedInfo.title}
            </ToggleButton>
          )) }
        </ToggleButtonGroup>
      </Menu>
    </Box>
  )
}