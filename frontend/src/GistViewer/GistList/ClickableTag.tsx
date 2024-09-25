import ToggleButton from '@mui/material/ToggleButton';
import { useAppDispatch, useAppSelector } from '../../store';
import { selectTags, tagToggled } from '../slice';


interface ClickableTagProps {
    tagText: string
}

export const ClickableTag = ({ tagText }: ClickableTagProps) => {
    const tags = useAppSelector(selectTags);
    const dispatch = useAppDispatch();

    const isToggled = tags.includes(tagText);

    return <ToggleButton 
        value={ tagText }
        selected={ isToggled }
        onClick={ () => {
            dispatch(tagToggled(tagText))
        } }
        size="small"
        sx={{
            fontSize: "0.7rem",
            py: "0.05rem",
            px: "0.3rem",
            my: "0.25rem",
            mr: "0.1rem",
        }}
        color="info"
    >
        { tagText }
    </ToggleButton>
}