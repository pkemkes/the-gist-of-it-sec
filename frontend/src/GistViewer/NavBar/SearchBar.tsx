import { IconButton, InputAdornment, TextField, Tooltip, useTheme } from "@mui/material"
import SearchIcon from "@mui/icons-material/Search";
import AutoAwesomeIcon from "@mui/icons-material/AutoAwesome";
import { useAppDispatch } from "../../store";
import { aiSearchQueryChanged, searchQueryChanged } from "../slice";
import { useEffect, useState } from "react";

export const SearchBar = () => {
  const [ searchQuery, setSearchQuery ] = useState("");
  const dispatch = useAppDispatch();
  const isLightMode = useTheme().palette.mode == "light";

  useEffect(() => {
    const debounceTimer = setTimeout(() => {
      dispatch(searchQueryChanged(searchQuery));
    }, 250);
    return () => {
      clearTimeout(debounceTimer);
    }
  }, [searchQuery]);

  const handleChange = (event: React.FormEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    setSearchQuery(event.currentTarget.value);
  };

  const handleAiSearch = () => {
    if (searchQuery.trim()) {
      dispatch(aiSearchQueryChanged(searchQuery.trim()));
    }
  };

	return <TextField 
		label="Search"
    size="small"
    sx={{ mr: "0.5rem", width: "20rem" }}
    value={ searchQuery }
    onChange={ handleChange }
    color={ isLightMode ? "secondary" : "primary" }
    slotProps={{
      input: {
        endAdornment: (
          <InputAdornment position="end">
            <Tooltip title="AI powered search">
              <IconButton size="small" aria-label="AI search" onClick={ handleAiSearch }>
                <AutoAwesomeIcon />
              </IconButton>
            </Tooltip>
            <SearchIcon />
          </InputAdornment>
        ),
      }
    }}
   />
}