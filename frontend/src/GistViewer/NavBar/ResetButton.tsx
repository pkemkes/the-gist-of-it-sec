import { IconButton, Tooltip } from "@mui/material"
import RefreshOutlinedIcon from '@mui/icons-material/RefreshOutlined';
import { useAppDispatch } from "../../store";
import { gistListReset } from "../slice"
import { backendApi } from "../../services/backend";


export const ResetButton = () => {
  const dispatch = useAppDispatch();

  return <Tooltip title="Reset" placement="bottom">
    <IconButton 
      onClick={() => {
        dispatch(backendApi.util.resetApiState());
        dispatch(gistListReset());
      }}
      sx={{ mr: "1rem" }}
      edge='start'
      color='inherit'
    >
      <RefreshOutlinedIcon />
    </IconButton>
  </Tooltip>
}