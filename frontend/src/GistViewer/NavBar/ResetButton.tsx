import { Box, IconButton, Tooltip } from "@mui/material"
import RefreshOutlinedIcon from "@mui/icons-material/RefreshOutlined";
import { useAppDispatch } from "../../store";
import { gistListReset } from "../slice"
import { backendApi } from "../../backend";


export const ResetButton = () => {
  const dispatch = useAppDispatch();

  return <Box sx={{ ml: "1rem", mr: "0.5rem" }}>
    <Tooltip title="Reset" placement="bottom">
      <IconButton 
        onClick={() => {
          dispatch(backendApi.util.resetApiState());
          dispatch(gistListReset());
        }}
        edge="start"
        color="inherit"
      >
        <RefreshOutlinedIcon />
      </IconButton>
    </Tooltip>
  </Box>;
}