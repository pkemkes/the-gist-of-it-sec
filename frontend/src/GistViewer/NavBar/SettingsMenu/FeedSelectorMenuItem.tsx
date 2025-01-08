import { disabledFeedIdsChanged, selectDisabledFeedIds } from "../../slice";
import { useAppDispatch, useAppSelector } from "../../../store";
import {
  CircularProgress, 
  FormControl, 
  InputLabel, 
  MenuItem, 
  OutlinedInput, 
  Select, 
  SelectChangeEvent,
  Typography
} from "@mui/material";
import { backendApi } from "../../../backend";
import { FeedInfo } from "src/types";
import { ShortenedTypography } from "./ShortenedTypography";


const ITEM_HEIGHT = 48;
const ITEM_PADDING_TOP = 8;
const MenuProps = {
  PaperProps: {
    style: {
      maxHeight: ITEM_HEIGHT * 4.5 + ITEM_PADDING_TOP,
      width: 250,
    },
  },
};


export const FeedSelectorMenuItem = () => {
  const dispatch = useAppDispatch();
  const disabledFeedIds = useAppSelector(selectDisabledFeedIds);
    
  const { data: feeds, error, isFetching } = backendApi.useGetAllFeedInfoQuery();

  if (isFetching) {
    return <MenuItem sx={{ display: "flex", flexDirection: "column" }}>
      <CircularProgress />
    </MenuItem>
  }

  if (feeds == undefined || error) {
    return <MenuItem>
      <Typography>
        An error occurred loading the feed info. Please try again later.
      </Typography>
    </MenuItem>
  }

  const toSortedFeeds = (feedsToSort: FeedInfo[]) => 
    feedsToSort.toSorted((a, b) => a.title.localeCompare(b.title));

  const filterFeedsByIds = (idsToExclude: number[]) => 
    feeds.filter(feed => !idsToExclude.includes(feed.id));

  const handleChange = (event: SelectChangeEvent<number[]>) => {
    const value = event.target.value;
    const selectedEnabledFeedIds = typeof value === "string"
      ? value.split(",").map(numString => parseInt(numString))
      : value;
    const disabledFeedIds = filterFeedsByIds(selectedEnabledFeedIds).map(feed => feed.id);
    dispatch(disabledFeedIdsChanged(disabledFeedIds));
  };

  const enabledFeeds = filterFeedsByIds(disabledFeedIds);

  const getFeedTitleForId = (feedId: number) => feeds.filter(feed => feed.id == feedId)[0].title;

  const renderSelected = (selectedValues: number[]) => {
    const combinedTitles = selectedValues.map(feedId => getFeedTitleForId(feedId)).toSorted().join(", ");
    return <ShortenedTypography value={ combinedTitles } />;
  };

  return (
    <MenuItem>
      <FormControl sx={{ my: "0.5rem", width: "20rem" }}>
        <InputLabel id="disabled-feeds-selector-label">Feeds</InputLabel>
        <Select
          labelId="disabled-feeds-selector-label"
          id="disabled-feeds-selector"
          multiple
          value={ enabledFeeds.map(feed => feed.id) }
          onChange={ handleChange }
          input={ <OutlinedInput id="select-multiple-chip" label="Feeds" /> }
          renderValue={ renderSelected }
          MenuProps={MenuProps}
        >
          { toSortedFeeds(feeds).map((feed) => (
            <MenuItem
              key={ feed.id }
              value={ feed.id }
            >
              <ShortenedTypography value={ feed.title } />
            </MenuItem>
          )) }
        </Select>
      </FormControl>
    </MenuItem>
  )
}