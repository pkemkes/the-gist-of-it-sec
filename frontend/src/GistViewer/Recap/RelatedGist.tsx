import { Link, ListItem } from "@mui/material"
import { useNavigate, Link as RouterLink } from "react-router";
import { RecapRelatedGist } from "src/types"


export const RelatedGist = ( { id, title }: RecapRelatedGist ) => {
	const navigate = useNavigate();
	
	return <ListItem sx={{ pr: 0, pb: 0, pl: "0.5rem" }}>
    <Link
      component={ RouterLink }
      to={ `/?gist=${id}` }
      underline="none"
    >
      • { title }
    </Link>
  </ListItem>
}